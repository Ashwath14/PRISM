using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using PRISM.App.Services;
using PRISM.App.ViewModels;
using PRISM.App.Views;
using PRISM.Hardware.Audio;
using PRISM.Hardware.Display;
using PRISM.Hardware.Storage;
using PRISM.Hardware.Windows;

namespace PRISM.App;

public partial class App : Application
{
    private static SingleInstanceService? _singleInstanceService;
    private TrayIconService? _trayIconService;
    private GlobalHotkeyService? _hotkeyService;
    private QuickControlFlyoutWindow? _flyoutWindow;
    private EnvironmentAutomationService? _automationService;
    private ProfileStorageService? _profileStorage;
    private MainViewModel? _mainVm;
    private MainWindow? _mainWindow;

    private static int _isShowingDialog = 0;
    private static string _lastErrorSignature = "";
    private static DateTime _lastErrorTime = DateTime.MinValue;

    public MainWindow EnsureMainWindow()
    {
        if (_mainWindow == null || _mainWindow.IsClosed || new WindowInteropHelper(_mainWindow).Handle == IntPtr.Zero)
        {
            StartupLogger.Log("EnsureMainWindow: Recreating MainWindow instance...");
            _mainWindow = new MainWindow(_mainVm!, _profileStorage!, _hotkeyService!, () => _flyoutWindow?.ShowFlyout());
            MainWindow = _mainWindow;
        }
        return _mainWindow;
    }

    private void TryRecoverMainView()
    {
        try
        {
            if (_mainWindow?.DataContext is MainViewModel mainVm)
            {
                if (mainVm.CurrentView != mainVm.HomeVm)
                {
                    StartupLogger.Log("[RECOVERY] Active view triggered layout failure. Gracefully recovering to HomeView.");
                    mainVm.Navigate("Home");
                }
            }
        }
        catch (Exception ex)
        {
            StartupLogger.Log($"[RECOVERY ERROR] Could not recover to HomeView: {ex.Message}");
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var args = Environment.GetCommandLineArgs();
        StartupLogger.Log($"Application entry point reached. Args: '{string.Join(" ", args)}'");

        // 1. Production Global Exception Trapping
        AppDomain.CurrentDomain.UnhandledException += (s, exArgs) =>
        {
            StartupLogger.Log($"[CRITICAL UNHANDLED DOMAIN EXCEPTION] IsTerminating={exArgs.IsTerminating}, Exception={exArgs.ExceptionObject}");
            try
            {
                System.Windows.MessageBox.Show(
                    $"PRISM Fatal Startup Error:\n{exArgs.ExceptionObject}", 
                    "PRISM Fatal Error", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
            }
            catch { }
        };

        TaskScheduler.UnobservedTaskException += (s, tArgs) =>
        {
            StartupLogger.Log($"[UNOBSERVED TASK EXCEPTION] {tArgs.Exception}");
            tArgs.SetObserved();
        };

        DispatcherUnhandledException += (s, exArgs) =>
        {
            StartupLogger.Log($"[DISPATCHER UNHANDLED EXCEPTION] {exArgs.Exception}");
            exArgs.Handled = true;

            string currentExSignature = $"{exArgs.Exception.GetType().FullName}:{exArgs.Exception.Message}";
            DateTime now = DateTime.UtcNow;

            // Prevent re-entrant and duplicate modal dialogs during layout / measure
            if (Interlocked.CompareExchange(ref _isShowingDialog, 1, 0) != 0)
            {
                StartupLogger.Log($"[DISPATCHER UNHANDLED EXCEPTION] Suppressed duplicate dialog for: {currentExSignature}");
                return;
            }

            if (string.Equals(_lastErrorSignature, currentExSignature, StringComparison.Ordinal) && (now - _lastErrorTime).TotalSeconds < 3)
            {
                StartupLogger.Log($"[DISPATCHER UNHANDLED EXCEPTION] Throttled recurring error dialog within 3s for: {currentExSignature}");
                Interlocked.Exchange(ref _isShowingDialog, 0);
                return;
            }

            _lastErrorSignature = currentExSignature;
            _lastErrorTime = now;

            // Attempt graceful navigation recovery back to HomeView
            TryRecoverMainView();

            // Post dialog asynchronously at ApplicationIdle priority so it never re-enters the active WPF layout pass
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    System.Windows.MessageBox.Show(
                        $"PRISM UI Error:\n{exArgs.Exception.Message}\n\nStack:\n{exArgs.Exception.StackTrace}",
                        "PRISM Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                finally
                {
                    Interlocked.Exchange(ref _isShowingDialog, 0);
                }
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        };

        // 2. Resource Verification
        try
        {
            int resCount = Current?.Resources?.Count ?? 0;
            var canvasBrush = Current?.TryFindResource("PrismCanvasBrush");
            StartupLogger.Log($"Resource initialization check: ResourcesCount={resCount}, PrismCanvasBrushResolved={canvasBrush != null}");
        }
        catch (Exception ex)
        {
            StartupLogger.Log($"[RESOURCE ERROR] {ex.Message}");
        }

        // 3. Single Instance Verification & IPC Registration
        _singleInstanceService = new SingleInstanceService(OnInstanceActivated);
        if (!_singleInstanceService.Initialize(args))
        {
            StartupLogger.Log("Application instance is secondary and signalled primary instance. Shutting down.");
            Shutdown(0);
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        StartupLogger.Log("ShutdownMode set to OnExplicitShutdown.");

        // 4. Instantiate Subsystems & Hardware Services
        DdcCiMonitorService displayService;
        CoreAudioDeviceService audioService;
        EqualizerApoBridgeService apoBridge;
        DisplayResolutionService resolutionService;
        DisplayAdvancedColorService advancedColorService;
        SpatialAudioService spatialAudioService;

        try
        {
            StartupLogger.Log("Initializing Hardware Services...");
            _profileStorage = new ProfileStorageService();
            displayService = new DdcCiMonitorService(null, _profileStorage);
            audioService = new CoreAudioDeviceService();
            apoBridge = new EqualizerApoBridgeService();
            resolutionService = new DisplayResolutionService();
            advancedColorService = new DisplayAdvancedColorService();
            spatialAudioService = new SpatialAudioService();
            _hotkeyService = new GlobalHotkeyService();
            _automationService = new EnvironmentAutomationService(_profileStorage, displayService, audioService, apoBridge);
            StartupLogger.Log("Hardware Services and Environment Automation initialized successfully.");
        }
        catch (Exception ex)
        {
            StartupLogger.Log($"[SERVICE INIT ERROR] {ex}");
            throw;
        }

        // 5. Instantiate ViewModels
        HomeViewModel homeVm;
        DisplayStudioViewModel displayVm;
        AudioStudioViewModel audioVm;
        EnvironmentsViewModel envVm;
        SettingsViewModel settingsVm;

        try
        {
            StartupLogger.Log("Initializing ViewModels...");
            homeVm = new HomeViewModel(displayService, audioService, _profileStorage);
            displayVm = new DisplayStudioViewModel(displayService, _profileStorage, resolutionService, advancedColorService);
            audioVm = new AudioStudioViewModel(audioService, apoBridge, _profileStorage, spatialAudioService);
            envVm = new EnvironmentsViewModel(_profileStorage, displayService, audioService, apoBridge, _profileStorage);
            settingsVm = new SettingsViewModel(displayService, audioService, apoBridge, _profileStorage);
            _mainVm = new MainViewModel(homeVm, displayVm, audioVm, envVm, settingsVm);
            StartupLogger.Log("ViewModels initialized successfully.");
        }
        catch (Exception ex)
        {
            StartupLogger.Log($"[VIEWMODEL INIT ERROR] {ex}");
            throw;
        }

        // 6. Instantiate Quick Controls Flyout Window
        try
        {
            StartupLogger.Log("Initializing QuickControlFlyoutWindow...");
            var flyoutVm = new QuickControlFlyoutViewModel(
                displayService,
                audioService,
                _profileStorage,
                apoBridge,
                dest =>
                {
                    var win = EnsureMainWindow();
                    win.RestoreAndActivate();
                    _mainVm.Navigate(dest);
                });

            _flyoutWindow = new QuickControlFlyoutWindow(flyoutVm);
            StartupLogger.Log("QuickControlFlyoutWindow created successfully.");
        }
        catch (Exception ex)
        {
            StartupLogger.Log($"[FLYOUT WINDOW INIT ERROR] {ex}");
            // Non-fatal: flyout failure should not block main studio
        }

        // 7. Instantiate Main Studio Window
        try
        {
            StartupLogger.Log("Initializing MainWindow...");
            EnsureMainWindow();
            StartupLogger.Log("MainWindow created successfully.");
        }
        catch (Exception ex)
        {
            StartupLogger.Log($"[MAINWINDOW INIT ERROR] {ex}");
            throw;
        }

        // 8. Instantiate System Tray Icon
        try
        {
            StartupLogger.Log("Initializing System Tray Icon...");
            _trayIconService = new TrayIconService(
                displayService,
                audioService,
                _profileStorage,
                apoBridge,
                () => EnsureMainWindow().RestoreAndActivate(),
                () => _flyoutWindow?.ShowFlyout(),
                () =>
                {
                    var win = EnsureMainWindow();
                    win.RestoreAndActivate();
                    _mainVm.Navigate("Settings");
                },
                () => _mainWindow?.ForceExit());
            StartupLogger.Log("System Tray Icon initialized successfully.");
        }
        catch (Exception ex)
        {
            StartupLogger.Log($"[TRAY ICON INIT ERROR] {ex}");
        }

        // 9. Process Standard CLI Navigation Arguments
        if (args.Any(a => a.Equals("--display", StringComparison.OrdinalIgnoreCase))) _mainVm.Navigate("Display");
        else if (args.Any(a => a.Equals("--audio", StringComparison.OrdinalIgnoreCase))) _mainVm.Navigate("Audio");
        else if (args.Any(a => a.Equals("--environments", StringComparison.OrdinalIgnoreCase))) _mainVm.Navigate("Environments");
        else if (args.Any(a => a.Equals("--settings", StringComparison.OrdinalIgnoreCase))) _mainVm.Navigate("Settings");
        else if (args.Any(a => a.Equals("--home", StringComparison.OrdinalIgnoreCase))) _mainVm.Navigate("Home");

        // 10. Display Main Studio Window
        bool startMinimized = args.Any(a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase));
        StartupLogger.Log($"Window launch evaluation: startMinimized={startMinimized}");

        if (!startMinimized)
        {
            try
            {
                StartupLogger.Log("Showing and activating MainWindow...");
                var win = EnsureMainWindow();
                win.RestoreAndActivate();
                var handle = new WindowInteropHelper(win).Handle;
                StartupLogger.Log($"MainWindow shown and activated! Visibility={win.Visibility}, IsVisible={win.IsVisible}, IsLoaded={win.IsLoaded}, HWND=0x{handle.ToInt64():X}");
            }
            catch (Exception ex)
            {
                StartupLogger.Log($"[MAINWINDOW SHOW ERROR] {ex}");
                throw;
            }
        }
    }

    private bool OnInstanceActivated(string[] args)
    {
        StartupLogger.Log($"OnInstanceActivated invoked. Args: '{string.Join(" ", args)}'");
        try
        {
            bool restored = false;
            long hwnd = 0;

            Dispatcher.Invoke(() =>
            {
                try
                {
                    if (_mainVm == null || _profileStorage == null || _hotkeyService == null)
                    {
                        StartupLogger.Log("OnInstanceActivated: Subsystems not yet initialized.");
                        return;
                    }

                    var win = EnsureMainWindow();
                    win.RestoreAndActivate();

                    if (args.Any(a => a.Equals("--display", StringComparison.OrdinalIgnoreCase))) win.Navigate("Display");
                    else if (args.Any(a => a.Equals("--audio", StringComparison.OrdinalIgnoreCase))) win.Navigate("Audio");
                    else if (args.Any(a => a.Equals("--environments", StringComparison.OrdinalIgnoreCase))) win.Navigate("Environments");
                    else if (args.Any(a => a.Equals("--settings", StringComparison.OrdinalIgnoreCase))) win.Navigate("Settings");
                    else if (args.Any(a => a.Equals("--home", StringComparison.OrdinalIgnoreCase))) win.Navigate("Home");

                    var handle = new WindowInteropHelper(win).Handle;
                    hwnd = handle.ToInt64();
                    restored = handle != IntPtr.Zero && win.IsVisible;
                }
                catch (Exception ex)
                {
                    StartupLogger.LogException("Error restoring MainWindow in OnInstanceActivated", ex);
                    restored = false;
                }
            });

            StartupLogger.Log($"OnInstanceActivated completed. restored={restored}, HWND=0x{hwnd:X}");
            return restored;
        }
        catch (Exception ex)
        {
            StartupLogger.LogException("OnInstanceActivated dispatcher invoke failed", ex);
            return false;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        StartupLogger.Log($"Application OnExit called with exit code {e.ApplicationExitCode}.");
        _automationService?.Dispose();
        _hotkeyService?.Dispose();
        _trayIconService?.Dispose();
        _flyoutWindow?.Close();

        _singleInstanceService?.Dispose();
        _singleInstanceService = null;

        base.OnExit(e);
    }
}
