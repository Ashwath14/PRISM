using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using PRISM.Core.Interfaces;
using PRISM.Core.Models;

namespace PRISM.Hardware.Audio;

public class SpatialAudioService : ISpatialAudioService
{
    public SpatialAudioInfo GetSpatialAudioInfo(string? deviceId = null)
    {
        var info = new SpatialAudioInfo();

        try
        {
            // 1. Check for installed Dolby Access application
            bool dolbyFound = false;

            // Check HKCU & HKLM software keys
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Dolby"))
            {
                if (key != null) dolbyFound = true;
            }
            if (!dolbyFound)
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"Software\Dolby"))
                {
                    if (key != null) dolbyFound = true;
                }
            }

            // Check WindowsApps directory or Package repository for Dolby Access AppX
            if (!dolbyFound)
            {
                string localAppx = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages");
                if (Directory.Exists(localAppx))
                {
                    var dolbyDirs = Directory.GetDirectories(localAppx, "*DolbyLaboratories*");
                    if (dolbyDirs.Length > 0) dolbyFound = true;
                }
            }

            info.IsDolbyAccessInstalled = dolbyFound;

            // Check for DTS Sound Unbound
            bool dtsFound = false;
            string packagesDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages");
            if (Directory.Exists(packagesDir))
            {
                var dtsDirs = Directory.GetDirectories(packagesDir, "*DTS*");
                if (dtsDirs.Length > 0) dtsFound = true;
            }
            info.IsDtsInstalled = dtsFound;

            // 2. Query Windows Spatial Sound configuration for current user
            // Windows stores spatial sound endpoint configuration in Audio key
            using (var audioKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Audio"))
            {
                if (audioKey != null)
                {
                    var val = audioKey.GetValue("SpatialAudioFormat");
                    if (val != null)
                    {
                        string sVal = val.ToString() ?? "";
                        if (sVal.Contains("Sonic", StringComparison.OrdinalIgnoreCase))
                        {
                            info.IsActive = true;
                            info.ActiveFormatName = "Windows Sonic for Headphones (Active)";
                        }
                        else if (sVal.Contains("Dolby", StringComparison.OrdinalIgnoreCase) || sVal.Contains("Atmos", StringComparison.OrdinalIgnoreCase))
                        {
                            info.IsActive = true;
                            info.ActiveFormatName = "Dolby Atmos for Headphones (Active)";
                        }
                        else if (sVal.Contains("DTS", StringComparison.OrdinalIgnoreCase))
                        {
                            info.IsActive = true;
                            info.ActiveFormatName = "DTS Headphone:X (Active)";
                        }
                    }
                }
            }

            if (!info.IsActive)
            {
                info.ActiveFormatName = "Off (Stereo / Direct Mode)";
                info.StatusMessage = info.IsDolbyAccessInstalled
                    ? "Dolby Access is installed. Activate Dolby Atmos in Windows Sound Settings."
                    : "Windows Sonic for Headphones available license-free in Windows Sound Settings.";
            }
            else
            {
                info.StatusMessage = $"Spatial Audio active ({info.ActiveFormatName}). Dynamic 3D soundstage enabled.";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SpatialAudioService Error] {ex.Message}");
            info.ActiveFormatName = "Direct Stereo (Standard)";
            info.StatusMessage = "Spatial Audio status query ready.";
        }

        return info;
    }

    public void OpenWindowsSoundSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "ms-settings:sound",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to open Windows Sound settings: {ex.Message}");
        }
    }

    public void OpenDolbyAccess()
    {
        try
        {
            // Launch Dolby Access via URI protocol
            Process.Start(new ProcessStartInfo
            {
                FileName = "ms-windows-store://pdp/?productid=9N0866FS04W8", // Dolby Access Store URI
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to launch Dolby Access: {ex.Message}");
            OpenWindowsSoundSettings();
        }
    }
}
