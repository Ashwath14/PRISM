using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using PRISM.Core.Common;

namespace PRISM.Hardware.Windows;

public static class WindowsStartupService
{
    private const string RUN_KEY = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string APP_NAME = "PRISM";

    public static bool IsStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RUN_KEY, false);
            if (key == null) return false;
            var val = key.GetValue(APP_NAME) as string;
            return !string.IsNullOrEmpty(val);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error checking startup registry: {ex.Message}");
            return false;
        }
    }

    public static Result SetStartup(bool enable, string? customExecutablePath = null)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RUN_KEY, true);
            if (key == null) return Result.Failure("Unable to open user Run registry key.");

            if (enable)
            {
                string exePath = customExecutablePath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "";
                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                {
                    // Fallback for dev environment or dotnet run
                    exePath = Environment.ProcessPath ?? "";
                }

                if (string.IsNullOrEmpty(exePath))
                {
                    return Result.Failure("Unable to resolve PRISM application executable path.");
                }

                // Add --minimized flag so PRISM launches silently to tray
                string command = $"\"{exePath}\" --minimized";
                key.SetValue(APP_NAME, command, RegistryValueKind.String);
                Debug.WriteLine($"[PRISM Windows Integration] Startup enabled: {command}");
            }
            else
            {
                if (key.GetValue(APP_NAME) != null)
                {
                    key.DeleteValue(APP_NAME, false);
                    Debug.WriteLine("[PRISM Windows Integration] Startup disabled.");
                }
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error updating startup registry: {ex.Message}");
            return Result.Failure($"Failed to configure startup: {ex.Message}");
        }
    }
}
