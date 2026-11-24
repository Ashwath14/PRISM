using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using PRISM.Core.Common;

namespace PRISM.Hardware.Windows;

public class GlobalHotkeyService : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_WIN = 0x0008;
    private const uint MOD_NOREPEAT = 0x4000;
    private const uint VK_P = 0x50;

    private const int PRISM_HOTKEY_ID = 9001;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private IntPtr _hwnd = IntPtr.Zero;
    private HwndSource? _hwndSource;
    private bool _isRegistered = false;

    public bool IsRegistered => _isRegistered;
    public string ShortcutText => "Win + Alt + P";

    public event Action? HotkeyPressed;

    public Result Register(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return Result.Failure("Invalid window handle for hotkey registration.");

        _hwnd = hwnd;
        _hwndSource = HwndSource.FromHwnd(hwnd);
        _hwndSource?.AddHook(HwndHook);

        bool success = RegisterHotKey(_hwnd, PRISM_HOTKEY_ID, MOD_WIN | MOD_ALT | MOD_NOREPEAT, VK_P);
        if (success)
        {
            _isRegistered = true;
            Debug.WriteLine("[PRISM Windows Integration] Global shortcut Win+Alt+P successfully registered.");
            return Result.Success();
        }
        else
        {
            int err = Marshal.GetLastWin32Error();
            Debug.WriteLine($"[PRISM Windows Integration] Failed to register Win+Alt+P. Error code: {err}");
            return Result.Failure($"Hotkey Win+Alt+P could not be registered (Error code: {err}). It may be reserved or in use by another application.");
        }
    }

    public void Unregister()
    {
        if (_isRegistered && _hwnd != IntPtr.Zero)
        {
            UnregisterHotKey(_hwnd, PRISM_HOTKEY_ID);
            _hwndSource?.RemoveHook(HwndHook);
            _isRegistered = false;
            Debug.WriteLine("[PRISM Windows Integration] Global shortcut Win+Alt+P unregistered.");
        }
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == PRISM_HOTKEY_ID)
        {
            handled = true;
            Debug.WriteLine("[PRISM Windows Integration] Win+Alt+P triggered!");
            HotkeyPressed?.Invoke();
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Unregister();
    }
}
