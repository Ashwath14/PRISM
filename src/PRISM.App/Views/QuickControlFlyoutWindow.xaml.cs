using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using PRISM.App.ViewModels;
using PRISM.Hardware.Display;

namespace PRISM.App.Views;

public partial class QuickControlFlyoutWindow : Window
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private readonly QuickControlFlyoutViewModel _viewModel;
    private bool _isClosingAnimated = false;

    public QuickControlFlyoutWindow(QuickControlFlyoutViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        _viewModel.RequestClose += () => CloseWithAnimation();
        _viewModel.CompactModeChanged += ApplyCompactModeLayout;

        KeyDown += OnWindowKeyDown;

        Deactivated += (s, e) =>
        {
            if (!_isClosingAnimated && IsVisible)
            {
                CloseWithAnimation();
            }
        };
    }

    private void ApplyCompactModeLayout()
    {
        if (_viewModel.IsCompactMode)
        {
            SubZeroRow.Visibility = Visibility.Collapsed;
            FooterLinksRow.Visibility = Visibility.Collapsed;
            Height = 350;
        }
        else
        {
            SubZeroRow.Visibility = Visibility.Visible;
            FooterLinksRow.Visibility = Visibility.Visible;
            Height = 540;
        }
        PositionNearTray();
    }

    private void OnWindowKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CloseWithAnimation();
            e.Handled = true;
        }
        else if (e.Key >= Key.D1 && e.Key <= Key.D4)
        {
            int idx = e.Key - Key.D1;
            _ = _viewModel.SelectEnvironmentByIndex(idx);
            e.Handled = true;
        }
        else if (e.Key >= Key.NumPad1 && e.Key <= Key.NumPad4)
        {
            int idx = e.Key - Key.NumPad1;
            _ = _viewModel.SelectEnvironmentByIndex(idx);
            e.Handled = true;
        }
        else if (e.Key == Key.M)
        {
            _ = _viewModel.ToggleMute();
            e.Handled = true;
        }
        else if (e.Key == Key.C)
        {
            _viewModel.ToggleCompactMode();
            e.Handled = true;
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var handle = new WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero)
        {
            int darkMode = 1;
            DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

            int backdrop = 3; // Acrylic
            DwmSetWindowAttribute(handle, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
        }
    }

    public void ShowFlyout()
    {
        _isClosingAnimated = false;
        _viewModel.SyncState();
        ApplyCompactModeLayout();
        PositionNearTray();

        // Animate appearance (Slide up and fade in)
        Opacity = 0.0;
        FlyoutTranslate.Y = 16;
        Show();
        Activate();

        var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(140))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        var slideUp = new DoubleAnimation(16, 0, TimeSpan.FromMilliseconds(140))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        BeginAnimation(OpacityProperty, fadeIn);
        FlyoutTranslate.BeginAnimation(TranslateTransform.YProperty, slideUp);

        var handle = new WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero)
        {
            SetForegroundWindow(handle);
        }
    }

    private void CloseWithAnimation()
    {
        if (_isClosingAnimated) return;
        _isClosingAnimated = true;

        var fadeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(100))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        fadeOut.Completed += (s, e) =>
        {
            Hide();
            _isClosingAnimated = false;
        };

        BeginAnimation(OpacityProperty, fadeOut);
    }

    public void PositionNearTray()
    {
        try
        {
            POINT pt;
            GetCursorPos(out pt);
            IntPtr hMonitor = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);

            var mi = new Dxva2Native.MONITORINFOEX();
            mi.cbSize = Marshal.SizeOf<Dxva2Native.MONITORINFOEX>();
            if (Dxva2Native.GetMonitorInfo(hMonitor, ref mi))
            {
                Left = mi.rcWork.Right - Width - 16;
                Top = mi.rcWork.Bottom - Height - 16;
                return;
            }
        }
        catch { }

        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 16;
        Top = workArea.Bottom - Height - 16;
    }
}
