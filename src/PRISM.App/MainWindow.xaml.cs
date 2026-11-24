using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using PRISM.App.Services;
using PRISM.App.ViewModels;
using PRISM.Core.Interfaces;
using PRISM.Hardware.Windows;

namespace PRISM.App;

public partial class MainWindow : Window
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
    private const int DWMWA_CAPTION_COLOR = 35;

    private const int WM_DISPLAYCHANGE = 0x007E;
    private const int WM_POWERBROADCAST = 0x0218;
    private const int PBT_APMRESUMEAUTOMATIC = 0x0012;
    private static readonly int WM_RESTORE_ACTIVATE = RegisterWindowMessage("PRISM_RESTORE_ACTIVATE");

    private const int SW_RESTORE = 9;
    private const int SW_SHOW = 5;
    private const uint MSGFLT_ALLOW = 1;

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_SHOWWINDOW = 0x0040;

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int RegisterWindowMessage(string lpString);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr ProcessId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ChangeWindowMessageFilterEx(IntPtr hWnd, uint msg, uint action, IntPtr pChangeFilterStruct);

    private readonly MainViewModel _viewModel;
    private readonly IEnvironmentService _environmentService;
    private readonly GlobalHotkeyService _hotkeyService;
    private readonly Action _openFlyoutAction;

    private bool _isExplicitExit = false;
    private bool _isClosed = false;

    public bool IsClosed => _isClosed;

    public MainWindow(
        MainViewModel viewModel,
        IEnvironmentService environmentService,
        GlobalHotkeyService hotkeyService,
        Action openFlyoutAction)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _environmentService = environmentService;
        _hotkeyService = hotkeyService;
        _openFlyoutAction = openFlyoutAction;
        DataContext = viewModel;

        Loaded += (s, e) => StartupLogger.Log($"MainWindow Loaded event fired. Visibility={Visibility}, IsVisible={IsVisible}, WindowState={WindowState}");
        ContentRendered += (s, e) => StartupLogger.Log($"MainWindow ContentRendered event fired. Visibility={Visibility}, IsVisible={IsVisible}, WindowState={WindowState}");
        Activated += (s, e) => StartupLogger.Log($"MainWindow Activated event fired.");
        Deactivated += (s, e) => StartupLogger.Log($"MainWindow Deactivated event fired.");
        StateChanged += (s, e) => StartupLogger.Log($"MainWindow StateChanged event fired. New WindowState={WindowState}");
        IsVisibleChanged += (s, e) => StartupLogger.Log($"MainWindow IsVisibleChanged event fired. New IsVisible={IsVisible}, Visibility={Visibility}");
        Closed += (s, e) => StartupLogger.Log($"MainWindow Closed event fired.");
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var handle = new WindowInteropHelper(this).Handle;
        StartupLogger.Log($"MainWindow OnSourceInitialized: HWND=0x{handle.ToInt64():X}");
        if (handle != IntPtr.Zero)
        {
            // Allow WM_RESTORE_ACTIVATE across UIPI boundary
            try
            {
                ChangeWindowMessageFilterEx(handle, (uint)WM_RESTORE_ACTIVATE, MSGFLT_ALLOW, IntPtr.Zero);
            }
            catch { }

            // Enable Windows 11 Dark Mode Title Bar
            int darkMode = 1;
            DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

            // Enable Windows 11 Mica / Acrylic Backdrop
            int backdrop = 2; // Mica
            DwmSetWindowAttribute(handle, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));

            // Caption Color (#0F172A in 0x00BBGGRR format)
            int captionColor = 0x002A170F;
            DwmSetWindowAttribute(handle, DWMWA_CAPTION_COLOR, ref captionColor, sizeof(int));

            // Register Global Win+Alt+P Hotkey
            var hotkeyResult = _hotkeyService.Register(handle);
            StartupLogger.Log($"Global hotkey registered: {hotkeyResult.IsSuccess}");
            if (hotkeyResult.IsSuccess)
            {
                _hotkeyService.HotkeyPressed += () => Dispatcher.Invoke(_openFlyoutAction);
            }

            // Hook Windows Message Queue for Display Topology & Sleep/Wake events
            var source = HwndSource.FromHwnd(handle);
            source?.AddHook(WndProc);
            StartupLogger.Log("WndProc message hook registered.");
        }
    }

    public void RestoreAndActivate()
    {
        StartupLogger.Log("MainWindow RestoreAndActivate called.");
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(RestoreAndActivate);
            return;
        }

        if (_isClosed)
        {
            StartupLogger.Log("MainWindow is closed; cannot restore this instance.");
            return;
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Visibility = Visibility.Visible;
        Show();

        var handle = new WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero)
        {
            try
            {
                IntPtr fgWindow = GetForegroundWindow();
                uint fgThread = GetWindowThreadProcessId(fgWindow, IntPtr.Zero);
                uint curThread = GetCurrentThreadId();

                if (fgThread != 0 && fgThread != curThread)
                {
                    AttachThreadInput(curThread, fgThread, true);
                    ShowWindow(handle, SW_RESTORE);
                    SetWindowPos(handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                    SetWindowPos(handle, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                    BringWindowToTop(handle);
                    SetForegroundWindow(handle);
                    AttachThreadInput(curThread, fgThread, false);
                }
                else
                {
                    ShowWindow(handle, SW_RESTORE);
                    SetWindowPos(handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                    SetWindowPos(handle, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                    BringWindowToTop(handle);
                    SetForegroundWindow(handle);
                }
            }
            catch (Exception ex)
            {
                StartupLogger.Log($"[RESTORE ERROR] Win32 activation error: {ex.Message}");
            }
        }

        Activate();
        Focus();

        StartupLogger.Log($"MainWindow RestoreAndActivate finished. Visibility={Visibility}, WindowState={WindowState}");
    }

    public void Navigate(string pageName)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => Navigate(pageName));
            return;
        }

        StartupLogger.Log($"MainWindow navigating to page: {pageName}");
        _viewModel.Navigate(pageName);
    }

    public void ForceExit()
    {
        _isExplicitExit = true;
        Close();
        Application.Current?.Shutdown(0);
    }

    private void OnFlyoutButtonClick(object sender, RoutedEventArgs e)
    {
        _openFlyoutAction?.Invoke();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        StartupLogger.Log($"MainWindow OnClosing invoked! _isExplicitExit={_isExplicitExit}");
        var settings = _environmentService.GetSettings();
        StartupLogger.Log($"MainWindow OnClosing settings: MinimizeToTray={settings.MinimizeToTray}");
        if (!_isExplicitExit && settings.MinimizeToTray)
        {
            e.Cancel = true;
            Hide();
            StartupLogger.Log("[PRISM Window] Minimized to system tray instead of closing.");
            return;
        }

        base.OnClosing(e);
        StartupLogger.Log("MainWindow base.OnClosing completed.");
    }

    protected override void OnClosed(EventArgs e)
    {
        _isClosed = true;
        base.OnClosed(e);
        StartupLogger.Log("MainWindow OnClosed fired.");
        var settings = _environmentService.GetSettings();
        if (_isExplicitExit || !settings.MinimizeToTray)
        {
            StartupLogger.Log("Explicit exit or MinimizeToTray=false; shutting down application.");
            Application.Current?.Shutdown(0);
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_RESTORE_ACTIVATE)
        {
            StartupLogger.Log("WndProc received WM_RESTORE_ACTIVATE message!");
            RestoreAndActivate();
            handled = true;
            return IntPtr.Zero;
        }

        if (msg == WM_DISPLAYCHANGE)
        {
            Debug.WriteLine("[PRISM Hardware Event] WM_DISPLAYCHANGE detected: Rediscovering display topology...");
            _viewModel.DisplayVm.RefreshHardwareCommand.Execute(null);
        }
        else if (msg == WM_POWERBROADCAST && wParam.ToInt32() == PBT_APMRESUMEAUTOMATIC)
        {
            Debug.WriteLine("[PRISM Power Event] System resumed from sleep: Restoring monitor connection...");
            _viewModel.DisplayVm.RefreshHardwareCommand.Execute(null);
        }

        return IntPtr.Zero;
    }
}
