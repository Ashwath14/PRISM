using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using PRISM.Core.Interfaces;

namespace PRISM.Hardware.Display;

public class SoftwareDimmerService : ISoftwareDimmerService
{
    private Window? _overlayWindow;
    private readonly object _lock = new();

    public void SetDimming(int screenX, int screenY, int screenWidth, int screenHeight, bool enabled, int percent)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            lock (_lock)
            {
                if (!enabled || percent <= 0)
                {
                    if (_overlayWindow != null)
                    {
                        _overlayWindow.Hide();
                    }
                    return;
                }

                if (_overlayWindow == null)
                {
                    _overlayWindow = CreateOverlayWindow();
                }

                // Position overlay window over the target monitor rect
                _overlayWindow.Left = screenX;
                _overlayWindow.Top = screenY;
                _overlayWindow.Width = screenWidth > 0 ? screenWidth : SystemParameters.PrimaryScreenWidth;
                _overlayWindow.Height = screenHeight > 0 ? screenHeight : SystemParameters.PrimaryScreenHeight;

                // Opacity curve: percent (0-100) mapped to maximum 0.85 opacity (prevents pitch blacklock)
                double targetOpacity = Math.Clamp((percent / 100.0) * 0.85, 0.0, 0.85);
                _overlayWindow.Opacity = targetOpacity;

                if (!_overlayWindow.IsVisible)
                {
                    _overlayWindow.Show();
                }
            }
        });
    }

    private Window CreateOverlayWindow()
    {
        var win = new Window
        {
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            Topmost = true,
            AllowsTransparency = true,
            Background = Brushes.Black,
            Focusable = false,
            ShowActivated = false
        };

        win.SourceInitialized += (s, e) =>
        {
            var hwnd = new WindowInteropHelper(win).Handle;
            if (hwnd != IntPtr.Zero)
            {
                // Make click-through: WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE
                int exStyle = Dxva2Native.GetWindowLong(hwnd, Dxva2Native.GWL_EXSTYLE);
                Dxva2Native.SetWindowLong(hwnd, Dxva2Native.GWL_EXSTYLE,
                    exStyle | Dxva2Native.WS_EX_LAYERED | Dxva2Native.WS_EX_TRANSPARENT | Dxva2Native.WS_EX_TOOLWINDOW | Dxva2Native.WS_EX_NOACTIVATE);
            }
        };

        return win;
    }

    public void ClearAll()
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            lock (_lock)
            {
                if (_overlayWindow != null)
                {
                    _overlayWindow.Hide();
                    _overlayWindow.Close();
                    _overlayWindow = null;
                }
            }
        });
    }

    public void Dispose()
    {
        ClearAll();
    }
}
