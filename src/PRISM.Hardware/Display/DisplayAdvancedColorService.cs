using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using PRISM.Core.Common;
using PRISM.Core.Interfaces;
using PRISM.Core.Models;

namespace PRISM.Hardware.Display;

public class DisplayAdvancedColorService : IDisplayAdvancedColorService
{
    private const int QDC_ONLY_ACTIVE_PATHS = 0x00000002;
    private const int ERROR_SUCCESS = 0;

    private const int DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO = 9;
    private const int DISPLAYCONFIG_DEVICE_INFO_SET_ADVANCED_COLOR_STATE = 11;

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_RATIONAL
    {
        public uint Numerator;
        public uint Denominator;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_SOURCE_INFO
    {
        public LUID adapterId;
        public uint id;
        public uint modeInfoIdx;
        public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_TARGET_INFO
    {
        public LUID adapterId;
        public uint id;
        public uint modeInfoIdx;
        public uint outputTechnology;
        public uint rotation;
        public uint scaling;
        public DISPLAYCONFIG_RATIONAL refreshRate;
        public uint scanLineOrdering;
        public bool targetAvailable;
        public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_INFO
    {
        public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
        public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
        public uint flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_MODE_INFO
    {
        public uint infoType;
        public uint id;
        public LUID adapterId;
        // Padding for union size
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public byte[] data;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_DEVICE_INFO_HEADER
    {
        public int type;
        public int size;
        public LUID adapterId;
        public uint id;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        public uint flags; // bits: 0: advancedColorSupported, 1: advancedColorEnabled, 2: wideColorEnforced, 3: advancedColorForceDisabled
        public int colorEncoding;
        public int bitsPerColorChannel;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        public uint flags; // bit 0: enableAdvancedColor
    }

    [DllImport("user32.dll")]
    private static extern int GetDisplayConfigBufferSizes(
        uint flags,
        out uint numPathArrayElements,
        out uint numModeInfoArrayElements);

    [DllImport("user32.dll")]
    private static extern int QueryDisplayConfig(
        uint flags,
        ref uint numPathArrayElements,
        [Out] DISPLAYCONFIG_PATH_INFO[] pathArray,
        ref uint numModeInfoArrayElements,
        [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray,
        IntPtr currentTopologyId);

    [DllImport("user32.dll")]
    private static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO requestPacket);

    [DllImport("user32.dll")]
    private static extern int DisplayConfigSetDeviceInfo(ref DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE setPacket);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetICMProfileW(
        IntPtr hdc,
        ref uint pcbData,
        StringBuilder? filename);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

    public DisplayHdrInfo GetAdvancedColorInfo(string? deviceName = null)
    {
        var info = new DisplayHdrInfo();

        try
        {
            // 1. Query Windows Image Color Management (ICM) Active Profile
            IntPtr hdc = GetDC(IntPtr.Zero);
            if (hdc != IntPtr.Zero)
            {
                try
                {
                    uint bufferSize = 0;
                    GetICMProfileW(hdc, ref bufferSize, null);
                    if (bufferSize > 0)
                    {
                        var sb = new StringBuilder((int)bufferSize);
                        if (GetICMProfileW(hdc, ref bufferSize, sb))
                        {
                            string icmPath = sb.ToString();
                            if (!string.IsNullOrWhiteSpace(icmPath))
                            {
                                info.IccProfilePath = System.IO.Path.GetFileName(icmPath);
                            }
                        }
                    }
                }
                finally
                {
                    ReleaseDC(IntPtr.Zero, hdc);
                }
            }

            // 2. Query Windows CCD Advanced Color via DisplayConfigGetDeviceInfo
            int err = GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out uint pathCount, out uint modeCount);
            if (err == ERROR_SUCCESS && pathCount > 0)
            {
                var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
                var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];

                err = QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
                if (err == ERROR_SUCCESS && pathCount > 0)
                {
                    var targetPath = paths[0];

                    var colorInfo = new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO
                    {
                        header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                        {
                            type = DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO,
                            size = Marshal.SizeOf<DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO>(),
                            adapterId = targetPath.targetInfo.adapterId,
                            id = targetPath.targetInfo.id
                        }
                    };

                    int getInfoResult = DisplayConfigGetDeviceInfo(ref colorInfo);
                    if (getInfoResult == ERROR_SUCCESS)
                    {
                        bool isSupported = (colorInfo.flags & 1) != 0;
                        bool isEnabled = (colorInfo.flags & 2) != 0;

                        info.IsHdrSupported = isSupported;
                        info.IsHdrEnabled = isEnabled;
                        info.BitsPerChannel = colorInfo.bitsPerColorChannel > 0 ? colorInfo.bitsPerColorChannel : 8;

                        info.ColorEncoding = colorInfo.colorEncoding switch
                        {
                            0 => "RGB",
                            1 => "YCbCr 4:4:4",
                            2 => "YCbCr 4:2:2",
                            3 => "YCbCr 4:2:0",
                            _ => "RGB (Standard)"
                        };

                        if (isSupported)
                        {
                            info.SummaryText = isEnabled 
                                ? $"HDR Active ({info.BitsPerChannel}-bit {info.ColorEncoding})" 
                                : $"HDR Supported (Currently Disabled in Windows)";
                        }
                        else
                        {
                            info.SummaryText = "SDR 8-bit Standard Dynamic Range";
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DisplayAdvancedColorService Error] {ex.Message}");
        }

        // Honest display hardware assessment
        info.DolbyVisionStatus = info.IsHdrSupported 
            ? "Requires Dolby Vision Certified Display & Store Extensions" 
            : "Unsupported on 8-bit SDR hardware (Acer ED270 X0)";

        return info;
    }

    public Task<Result> SetHdrEnabledAsync(bool enabled, string? deviceName = null)
    {
        try
        {
            int err = GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out uint pathCount, out uint modeCount);
            if (err != ERROR_SUCCESS || pathCount == 0)
            {
                return Task.FromResult(Result.Failure("Failed to acquire Windows display configuration paths."));
            }

            var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
            var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];

            err = QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
            if (err != ERROR_SUCCESS || pathCount == 0)
            {
                return Task.FromResult(Result.Failure("Failed to query active Windows display configuration."));
            }

            var targetPath = paths[0];

            var setPacket = new DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE
            {
                header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                {
                    type = DISPLAYCONFIG_DEVICE_INFO_SET_ADVANCED_COLOR_STATE,
                    size = Marshal.SizeOf<DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE>(),
                    adapterId = targetPath.targetInfo.adapterId,
                    id = targetPath.targetInfo.id
                },
                flags = enabled ? 1u : 0u
            };

            int setResult = DisplayConfigSetDeviceInfo(ref setPacket);
            if (setResult == ERROR_SUCCESS)
            {
                return Task.FromResult(Result.Success());
            }

            return Task.FromResult(Result.Failure($"Windows rejected Advanced Color state toggle (Win32 Code: {setResult}). Display hardware may not support HDR."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result.Failure($"Exception setting Advanced Color state: {ex.Message}"));
        }
    }
}
