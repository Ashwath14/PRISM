using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using PRISM.Core.Common;
using PRISM.Core.Interfaces;
using PRISM.Core.Models;

namespace PRISM.Hardware.Display;

public class DisplayResolutionService : IDisplayResolutionService, IDisposable
{
    private const int ENUM_CURRENT_SETTINGS = -1;
    private const int ENUM_REGISTRY_SETTINGS = -2;

    private const int CDS_UPDATEREGISTRY = 0x00000001;
    private const int CDS_TEST = 0x00000002;
    private const int CDS_FULLSCREEN = 0x00000004;

    private const int DISP_CHANGE_SUCCESSFUL = 0;
    private const int DISP_CHANGE_RESTART = 1;
    private const int DISP_CHANGE_FAILED = -1;
    private const int DISP_CHANGE_BADMODE = -2;
    private const int DISP_CHANGE_NOTUPDATED = -3;
    private const int DISP_CHANGE_BADFLAGS = -4;
    private const int DISP_CHANGE_BADPARAM = -5;

    private const int DM_BITSPERPEL = 0x00040000;
    private const int DM_PELSWIDTH = 0x00080000;
    private const int DM_PELSHEIGHT = 0x00100000;
    private const int DM_DISPLAYFREQUENCY = 0x00400000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DEVMODEW
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public ushort dmSpecVersion;
        public ushort dmDriverVersion;
        public ushort dmSize;
        public ushort dmDriverExtra;
        public uint dmFields;

        public int dmPositionX;
        public int dmPositionY;
        public uint dmDisplayOrientation;
        public uint dmDisplayFixedOutput;

        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        public ushort dmLogPixels;
        public uint dmBitsPerPel;
        public uint dmPelsWidth;
        public uint dmPelsHeight;
        public uint dmDisplayFlags;
        public uint dmDisplayFrequency;
        public uint dmICMMethod;
        public uint dmICMIntent;
        public uint dmMediaType;
        public uint dmDitherType;
        public uint dmReserved1;
        public uint dmReserved2;
        public uint dmPanningWidth;
        public uint dmPanningHeight;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DISPLAY_DEVICEW
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
        public int StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplayDevicesW(
        string? lpDevice,
        uint iDevNum,
        ref DISPLAY_DEVICEW lpDisplayDevice,
        uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplaySettingsExW(
        string? lpszDeviceName,
        int iModeNum,
        ref DEVMODEW lpDevMode,
        uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ChangeDisplaySettingsExW(
        string? lpszDeviceName,
        ref DEVMODEW lpDevMode,
        IntPtr hwnd,
        uint dwflags,
        IntPtr lParam);

    private readonly object _lock = new();
    private DisplayModeInfo? _originalMode;
    private DisplayModeInfo? _pendingMode;
    private System.Threading.Timer? _rollbackTimer;

    public bool IsRollbackPending { get; private set; }

    public event Action? RollbackTriggered;

    public string ResolveDeviceName(string? deviceName = null)
    {
        if (!string.IsNullOrWhiteSpace(deviceName)) return deviceName;

        try
        {
            var dd = new DISPLAY_DEVICEW { cb = Marshal.SizeOf<DISPLAY_DEVICEW>() };
            for (uint i = 0; EnumDisplayDevicesW(null, i, ref dd, 0); i++)
            {
                // DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x01
                if ((dd.StateFlags & 0x01) != 0)
                {
                    return dd.DeviceName;
                }
            }
        }
        catch { }

        return @"\\.\DISPLAY1";
    }

    public IReadOnlyList<DisplayModeInfo> GetSupportedModes(string? deviceName = null)
    {
        lock (_lock)
        {
            string targetDevice = ResolveDeviceName(deviceName);
            var rawModes = new List<DisplayModeInfo>();
            var dm = new DEVMODEW { dmSize = (ushort)Marshal.SizeOf<DEVMODEW>() };

            // 1. Get current active mode
            var currentMode = GetCurrentMode(targetDevice);

            // 2. Enumerate all supported graphics modes from GPU driver
            int modeNum = 0;
            while (EnumDisplaySettingsExW(targetDevice, modeNum, ref dm, 0))
            {
                // Only consider 32-bit true color modes for modern display quality
                if (dm.dmBitsPerPel >= 32 && dm.dmPelsWidth >= 800 && dm.dmPelsHeight >= 600)
                {
                    bool isActive = currentMode != null &&
                                    currentMode.Width == (int)dm.dmPelsWidth &&
                                    currentMode.Height == (int)dm.dmPelsHeight &&
                                    currentMode.RefreshRate == (int)dm.dmDisplayFrequency;

                    rawModes.Add(new DisplayModeInfo
                    {
                        Width = (int)dm.dmPelsWidth,
                        Height = (int)dm.dmPelsHeight,
                        RefreshRate = (int)dm.dmDisplayFrequency,
                        BitsPerPixel = (int)dm.dmBitsPerPel,
                        IsActive = isActive
                    });
                }
                modeNum++;
            }

            // Deduplicate modes by Width, Height, RefreshRate
            var uniqueModes = rawModes
                .GroupBy(m => new { m.Width, m.Height, m.RefreshRate })
                .Select(g => g.First())
                .OrderByDescending(m => m.Width * m.Height)
                .ThenByDescending(m => m.RefreshRate)
                .ToList();

            // Detect native resolution: For Acer ED270 X0 physical monitor, native panel is 1920x1080.
            // Any mode higher than 1920x1080 is GPU Virtual Super Resolution (VSR), not native panel.
            bool has1080p = uniqueModes.Any(m => m.Width == 1920 && m.Height == 1080);
            int nativeWidth = has1080p ? 1920 : (uniqueModes.Count > 0 ? uniqueModes.Max(m => m.Width) : 1920);
            int nativeHeight = has1080p ? 1080 : (uniqueModes.Count > 0 ? uniqueModes.Where(m => m.Width == nativeWidth).Max(m => m.Height) : 1080);

            foreach (var m in uniqueModes)
            {
                if (m.Width == nativeWidth && m.Height == nativeHeight)
                {
                    m.IsNative = true;
                }
            }

            return uniqueModes;
        }
    }

    public DisplayModeInfo? GetCurrentMode(string? deviceName = null)
    {
        string targetDevice = ResolveDeviceName(deviceName);
        var dm = new DEVMODEW { dmSize = (ushort)Marshal.SizeOf<DEVMODEW>() };
        if (EnumDisplaySettingsExW(targetDevice, ENUM_CURRENT_SETTINGS, ref dm, 0))
        {
            return new DisplayModeInfo
            {
                Width = (int)dm.dmPelsWidth,
                Height = (int)dm.dmPelsHeight,
                RefreshRate = (int)dm.dmDisplayFrequency,
                BitsPerPixel = (int)dm.dmBitsPerPel,
                IsNative = dm.dmPelsWidth == 1920 && dm.dmPelsHeight == 1080,
                IsActive = true
            };
        }
        return null;
    }

    public DisplayModeInfo? GetOriginalMode(string? deviceName = null)
    {
        lock (_lock)
        {
            return _originalMode ?? GetCurrentMode(deviceName);
        }
    }

    public Task<Result> ApplyModeAsync(DisplayModeInfo targetMode, string? deviceName = null)
    {
        lock (_lock)
        {
            try
            {
                string targetDevice = ResolveDeviceName(deviceName);

                // Record original mode before first modification
                if (_originalMode == null)
                {
                    _originalMode = GetCurrentMode(targetDevice);
                }

                var dm = new DEVMODEW
                {
                    dmSize = (ushort)Marshal.SizeOf<DEVMODEW>(),
                    dmPelsWidth = (uint)targetMode.Width,
                    dmPelsHeight = (uint)targetMode.Height,
                    dmDisplayFrequency = (uint)targetMode.RefreshRate,
                    dmBitsPerPel = (uint)targetMode.BitsPerPixel,
                    dmFields = DM_PELSWIDTH | DM_PELSHEIGHT | DM_DISPLAYFREQUENCY | DM_BITSPERPEL
                };

                // Apply dynamically and update registry
                int applyResult = ChangeDisplaySettingsExW(targetDevice, ref dm, IntPtr.Zero, CDS_UPDATEREGISTRY, IntPtr.Zero);
                if (applyResult == DISP_CHANGE_SUCCESSFUL)
                {
                    // Read back verified mode
                    var verified = GetCurrentMode(targetDevice);
                    _pendingMode = verified ?? targetMode;
                    IsRollbackPending = true;

                    // Start 15-second safety rollback timer
                    _rollbackTimer?.Dispose();
                    _rollbackTimer = new System.Threading.Timer(
                        _ => OnRollbackTimerElapsed(targetDevice),
                        null,
                        TimeSpan.FromSeconds(15),
                        Timeout.InfiniteTimeSpan);

                    return Task.FromResult(Result.Success());
                }

                // If updating registry directly failed, attempt dynamic change with 0
                int dynamicResult = ChangeDisplaySettingsExW(targetDevice, ref dm, IntPtr.Zero, 0, IntPtr.Zero);
                if (dynamicResult == DISP_CHANGE_SUCCESSFUL)
                {
                    var verified = GetCurrentMode(targetDevice);
                    _pendingMode = verified ?? targetMode;
                    IsRollbackPending = true;

                    _rollbackTimer?.Dispose();
                    _rollbackTimer = new System.Threading.Timer(
                        _ => OnRollbackTimerElapsed(targetDevice),
                        null,
                        TimeSpan.FromSeconds(15),
                        Timeout.InfiniteTimeSpan);

                    return Task.FromResult(Result.Success());
                }

                return Task.FromResult(Result.Failure(TranslateErrorCode(applyResult)));
            }
            catch (Exception ex)
            {
                return Task.FromResult(Result.Failure($"Failed to apply display mode: {ex.Message}"));
            }
        }
    }

    public void ConfirmAppliedMode()
    {
        lock (_lock)
        {
            _rollbackTimer?.Dispose();
            _rollbackTimer = null;
            IsRollbackPending = false;
            _originalMode = _pendingMode; // Update established baseline
            _pendingMode = null;
        }
    }

    public Task<Result> RevertModeAsync(string? deviceName = null)
    {
        lock (_lock)
        {
            _rollbackTimer?.Dispose();
            _rollbackTimer = null;
            IsRollbackPending = false;

            if (_originalMode == null)
            {
                return Task.FromResult(Result.Success());
            }

            string targetDevice = ResolveDeviceName(deviceName);
            var dm = new DEVMODEW
            {
                dmSize = (ushort)Marshal.SizeOf<DEVMODEW>(),
                dmPelsWidth = (uint)_originalMode.Width,
                dmPelsHeight = (uint)_originalMode.Height,
                dmDisplayFrequency = (uint)_originalMode.RefreshRate,
                dmBitsPerPel = (uint)_originalMode.BitsPerPixel,
                dmFields = DM_PELSWIDTH | DM_PELSHEIGHT | DM_DISPLAYFREQUENCY | DM_BITSPERPEL
            };

            int res = ChangeDisplaySettingsExW(targetDevice, ref dm, IntPtr.Zero, CDS_UPDATEREGISTRY, IntPtr.Zero);
            _pendingMode = null;

            if (res == DISP_CHANGE_SUCCESSFUL)
            {
                return Task.FromResult(Result.Success());
            }

            // Fallback dynamic revert
            res = ChangeDisplaySettingsExW(targetDevice, ref dm, IntPtr.Zero, 0, IntPtr.Zero);
            if (res == DISP_CHANGE_SUCCESSFUL)
            {
                return Task.FromResult(Result.Success());
            }

            return Task.FromResult(Result.Failure(TranslateErrorCode(res)));
        }
    }

    private void OnRollbackTimerElapsed(string? deviceName)
    {
        Debug.WriteLine("[DisplayResolutionService] Safety countdown elapsed without confirmation. Triggering automatic rollback.");
        _ = RevertModeAsync(deviceName);
        RollbackTriggered?.Invoke();
    }

    private static string TranslateErrorCode(int code) => code switch
    {
        DISP_CHANGE_BADMODE => "The display mode is not supported by your monitor or graphics driver.",
        DISP_CHANGE_FAILED => "The graphics driver failed the specified mode change.",
        DISP_CHANGE_RESTART => "A system restart is required to activate this display resolution.",
        DISP_CHANGE_BADFLAGS => "Invalid display mode flags passed to Windows display driver.",
        DISP_CHANGE_BADPARAM => "Invalid parameter structure for the selected display mode.",
        _ => $"Display mode change failed with Windows error code {code}."
    };

    public void Dispose()
    {
        _rollbackTimer?.Dispose();
    }
}
