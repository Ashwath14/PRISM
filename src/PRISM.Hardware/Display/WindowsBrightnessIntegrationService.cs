using System;
using System.Diagnostics;
using System.Management;
using System.Threading.Tasks;
using PRISM.Core.Common;
using PRISM.Core.Interfaces;
using PRISM.Core.Models;

namespace PRISM.Hardware.Display;

public class WindowsBrightnessIntegrationService : IWindowsBrightnessService
{
    private readonly object _lock = new();
    public event EventHandler<int>? ExternalWindowsBrightnessChanged;

    public WindowsBrightnessCapability QueryCapabilities(string? monitorHardwareKey = null)
    {
        var cap = new WindowsBrightnessCapability();

        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT * FROM WmiMonitorBrightness");
            using var results = searcher.Get();

            ManagementObject? activeObj = null;
            foreach (ManagementObject mo in results)
            {
                bool active = mo["Active"] != null && (bool)mo["Active"];
                if (active)
                {
                    activeObj = mo;
                    break;
                }
            }

            if (activeObj != null)
            {
                // Internal display with ACPI/WMI brightness support (e.g. laptop panel)
                cap.IsInternalDisplay = true;
                cap.IsQuickSettingsSyncSupported = true;
                cap.SupportType = WindowsBrightnessSupportType.SupportedInternalWmi;
                cap.DeviceInstanceId = activeObj["InstanceName"]?.ToString() ?? "Internal Display";
                cap.DisplayName = "Internal Integrated Display";
                cap.CurrentWindowsBrightness = Convert.ToInt32(activeObj["CurrentBrightness"] ?? -1);
                cap.RecommendedInterface = "WMI ACPI Backlight (root\\wmi\\WmiMonitorBrightnessMethods)";
                cap.TechnicalLimitationExplanation = "Supported via WDDM internal backlight ACPI interface.";
            }
            else
            {
                // External desktop monitor over DisplayPort / HDMI
                cap.IsInternalDisplay = false;
                cap.IsQuickSettingsSyncSupported = false;
                cap.SupportType = WindowsBrightnessSupportType.UnsupportedExternalDdcCi;
                cap.DisplayName = "Acer ED270 X0 (DisplayPort)";
                cap.DeviceInstanceId = @"DISPLAY\ACR0F95";
                cap.CurrentWindowsBrightness = -1;
                cap.RecommendedInterface = "Hardware DDC/CI (I2C VCP Opcode 0x10)";
                cap.TechnicalLimitationExplanation = 
                    "External DisplayPort monitors communicate via physical DDC/CI (I2C VCP opcode 0x10). " +
                    "Windows Quick Settings natively requires ACPI WDDM internal backlight support (WmiMonitorBrightness), " +
                    "which Microsoft does not expose for external desktop monitors. PRISM controls hardware luminance directly via DXVA2.";
            }
        }
        catch (ManagementException)
        {
            // ManagementStatus.NotFound or NotSupported is the standard WMI response when no internal panel exists
            cap.IsInternalDisplay = false;
            cap.IsQuickSettingsSyncSupported = false;
            cap.SupportType = WindowsBrightnessSupportType.UnsupportedExternalDdcCi;
            cap.DisplayName = "Acer ED270 X0 (DisplayPort)";
            cap.DeviceInstanceId = @"DISPLAY\ACR0F95";
            cap.CurrentWindowsBrightness = -1;
            cap.RecommendedInterface = "Hardware DDC/CI (I2C VCP Opcode 0x10)";
            cap.TechnicalLimitationExplanation =
                "External DisplayPort monitors communicate via physical DDC/CI (I2C VCP opcode 0x10). " +
                "Windows Quick Settings natively requires ACPI WDDM internal backlight support (WmiMonitorBrightness), " +
                "which Microsoft does not expose for external desktop monitors. PRISM controls hardware luminance directly via DXVA2.";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WMI Brightness Query Exception: {ex.Message}");
            cap.IsInternalDisplay = false;
            cap.IsQuickSettingsSyncSupported = false;
            cap.SupportType = WindowsBrightnessSupportType.Unavailable;
            cap.TechnicalLimitationExplanation = $"WMI query error: {ex.Message}";
        }

        return cap;
    }

    public Task<Result<int>> GetWindowsBrightnessAsync(string? monitorHardwareKey = null)
    {
        var cap = QueryCapabilities(monitorHardwareKey);
        if (!cap.IsQuickSettingsSyncSupported)
        {
            return Task.FromResult(Result<int>.Failure(cap.TechnicalLimitationExplanation));
        }

        return Task.FromResult(Result<int>.Success(cap.CurrentWindowsBrightness));
    }

    public async Task<Result> SetWindowsBrightnessAsync(int brightnessPercent, string? monitorHardwareKey = null)
    {
        var cap = QueryCapabilities(monitorHardwareKey);
        if (!cap.IsQuickSettingsSyncSupported)
        {
            return Result.Failure(cap.TechnicalLimitationExplanation);
        }

        int clamped = Math.Clamp(brightnessPercent, 0, 100);

        return await Task.Run(() =>
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT * FROM WmiMonitorBrightnessMethods");
                using var results = searcher.Get();

                foreach (ManagementObject mo in results)
                {
                    bool active = mo["Active"] != null && (bool)mo["Active"];
                    if (active)
                    {
                        mo.InvokeMethod("WmiSetBrightness", new object[] { 1, (byte)clamped });
                        ExternalWindowsBrightnessChanged?.Invoke(this, clamped);
                        return Result.Success();
                    }
                }

                return Result.Failure("No active WmiMonitorBrightnessMethods found.");
            }
            catch (Exception ex)
            {
                return Result.Failure($"Failed to set Windows native brightness: {ex.Message}");
            }
        });
    }
}
