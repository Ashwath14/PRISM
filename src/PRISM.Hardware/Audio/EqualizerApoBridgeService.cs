using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Win32;
using PRISM.Core.Common;
using PRISM.Core.Interfaces;
using PRISM.Core.Models;
using Path = System.IO.Path;
using File = System.IO.File;
using Directory = System.IO.Directory;

namespace PRISM.Hardware.Audio;

public class EqualizerApoBridgeService : IEqualizerApoBridge
{
    private const string APO_DEFAULT_PATH = @"C:\Program Files\EqualizerAPO";
    private const string REG_APO_KEY = @"SOFTWARE\EqualizerAPO";
    public const string PRISM_INCLUDE_FILE = "prism_dsp.txt";
    public const string MASTER_CONFIG_FILE = "config.txt";

    public EqualizerApoStatus CheckStatus(string? targetDeviceName = null)
    {
        var status = new EqualizerApoStatus();

        try
        {
            // 1. Check Windows Registry
            string? installPath = null;
            using (var key = Registry.LocalMachine.OpenSubKey(REG_APO_KEY))
            {
                if (key != null)
                {
                    installPath = key.GetValue("InstallPath") as string;
                    status.Version = key.GetValue("Version") as string ?? "1.4.x";
                }
            }

            // 2. Check standard directory
            if (string.IsNullOrEmpty(installPath) && Directory.Exists(APO_DEFAULT_PATH))
            {
                installPath = APO_DEFAULT_PATH;
            }

            if (!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath))
            {
                status.IsInstalled = true;
                status.InstallPath = installPath;
                string configDir = Path.Combine(installPath, "config");
                status.ConfigFilePath = Path.Combine(configDir, MASTER_CONFIG_FILE);

                bool isHooked = IsDeviceHookedByEqualizerApo(targetDeviceName);
                status.IsConfiguredForActiveDevice = isHooked;

                if (File.Exists(status.ConfigFilePath) && isHooked)
                {
                    status.StatusMessage = "Equalizer APO active and hooked into Windows Audio Engine for this endpoint.";
                    status.GuidanceTip = "PRISM filters are applied across all Windows applications in real-time.";
                }
                else if (File.Exists(status.ConfigFilePath) && !isHooked)
                {
                    status.StatusMessage = "Equalizer APO installed, but active audio endpoint is not enabled in Configurator.";
                    status.GuidanceTip = "Open Equalizer APO Configurator (C:\\Program Files\\EqualizerAPO\\Configurator.exe), check your active device, and restart the Windows Audio Service.";
                }
                else
                {
                    status.StatusMessage = "Equalizer APO installed, but config.txt is missing.";
                    status.GuidanceTip = "Run the Equalizer APO Configurator to register your playback endpoint.";
                }
            }
            else
            {
                status.IsInstalled = false;
                status.StatusMessage = "Equalizer APO is not installed on this system.";
                status.GuidanceTip = "Install Equalizer APO to unlock system-wide parametric DSP and graphic EQ.";
            }
        }
        catch (Exception ex)
        {
            status.IsInstalled = false;
            status.StatusMessage = $"Equalizer APO detection error: {ex.Message}";
        }

        return status;
    }

    public bool IsDeviceHookedByEqualizerApo(string? deviceName = null)
    {
        try
        {
            const string renderKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render";
            using var renderKey = Registry.LocalMachine.OpenSubKey(renderKeyPath);
            if (renderKey == null) return false;

            foreach (var subKeyName in renderKey.GetSubKeyNames())
            {
                using var devKey = renderKey.OpenSubKey(subKeyName);
                if (devKey == null) continue;

                // Check device friendly name if specified
                if (!string.IsNullOrEmpty(deviceName))
                {
                    using var propsKey = devKey.OpenSubKey("Properties");
                    if (propsKey != null)
                    {
                        string? name = propsKey.GetValue("{a45c254e-df1c-4efd-8020-67d146a850e0},2") as string;
                        if (!string.IsNullOrEmpty(name) && 
                            !name.Contains(deviceName, StringComparison.OrdinalIgnoreCase) && 
                            !deviceName.Contains(name, StringComparison.OrdinalIgnoreCase))
                        {
                            continue; // Skip non-matching endpoints
                        }
                    }
                }

                using var fxKey = devKey.OpenSubKey("FxProperties");
                if (fxKey != null)
                {
                    foreach (var valName in fxKey.GetValueNames())
                    {
                        string? val = fxKey.GetValue(valName)?.ToString();
                        if (!string.IsNullOrEmpty(val) && val.Contains("EACD2258-FC30-4D54-B746-4EE238E66858", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"MMDevice APO Hook Check Exception: {ex.Message}");
        }

        return false;
    }

    public string GenerateConfigString(AudioProfile profile, string? targetDeviceName = null)
    {
        var sb = new StringBuilder();
        var inv = CultureInfo.InvariantCulture;

        sb.AppendLine("# =============================================================================");
        sb.AppendLine("# PRISM Audio Studio DSP Configuration");
        sb.AppendLine($"# Profile: {profile.Name} | Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        if (!string.IsNullOrWhiteSpace(targetDeviceName ?? profile.TargetDeviceName))
        {
            sb.AppendLine($"# Target Device: {targetDeviceName ?? profile.TargetDeviceName}");
        }
        sb.AppendLine("# =============================================================================");
        sb.AppendLine();

        // Target Device Directive if specified
        string? activeTarget = targetDeviceName ?? profile.TargetDeviceName;
        if (!string.IsNullOrWhiteSpace(activeTarget))
        {
            sb.AppendLine($"Device: \"{activeTarget}\"");
            sb.AppendLine();
        }

        // Bypass check
        if (profile.IsBypassed)
        {
            sb.AppendLine("# DSP BYPASS MODE ACTIVE (A/B Testing - Pure Passthrough)");
            sb.AppendLine("Preamp: 0.0 dB");
            return sb.ToString();
        }

        // Calculate Auto-Preamp Headroom (Loudness Normalization)
        double maxPositiveGain = 0.0;
        if (profile.BandGains != null)
        {
            foreach (var g in profile.BandGains)
            {
                if (g > maxPositiveGain) maxPositiveGain = g;
            }
        }

        if (profile.BassGainDb > maxPositiveGain) maxPositiveGain = profile.BassGainDb;
        if (profile.TrebleGainDb > maxPositiveGain) maxPositiveGain = profile.TrebleGainDb;

        if (profile.ParametricFilters != null)
        {
            foreach (var pf in profile.ParametricFilters.Where(f => f.IsEnabled))
            {
                if (pf.GainDb > maxPositiveGain) maxPositiveGain = pf.GainDb;
            }
        }

        double effectivePreamp = profile.PreampDb;
        if (profile.IsLoudnessNormalizationEnabled && maxPositiveGain > 0.0)
        {
            // Attenuate by the maximum boost to eliminate inter-sample clipping
            effectivePreamp -= maxPositiveGain;
            sb.AppendLine($"# Auto-Preamp Normalization: -{maxPositiveGain.ToString("F1", inv)} dB headroom applied to prevent clipping");
        }

        if (Math.Abs(effectivePreamp) > 0.01)
        {
            sb.AppendLine($"Preamp: {effectivePreamp.ToString("F1", inv)} dB");
            sb.AppendLine();
        }

        // 10-Band Graphic Equalizer Line
        int[] freqs = { 32, 64, 125, 250, 500, 1000, 2000, 4000, 8000, 16000 };
        sb.Append("GraphicEQ: ");
        for (int i = 0; i < freqs.Length; i++)
        {
            double gain = (profile.BandGains != null && i < profile.BandGains.Length) ? profile.BandGains[i] : 0.0;
            sb.Append($"{freqs[i]} {gain.ToString("F1", inv)}");
            if (i < freqs.Length - 1) sb.Append("; ");
        }
        sb.AppendLine();
        sb.AppendLine();

        // Acoustic Enhancement Shelves
        if (Math.Abs(profile.BassGainDb) > 0.01)
        {
            sb.AppendLine($"# Bass Enhancement Low-Shelf (100 Hz)");
            sb.AppendLine($"Filter: ON LSC Fc 100 Hz Gain {profile.BassGainDb.ToString("F1", inv)} dB");
        }

        if (Math.Abs(profile.TrebleGainDb) > 0.01)
        {
            sb.AppendLine($"# Treble Air High-Shelf (10 kHz)");
            sb.AppendLine($"Filter: ON HSC Fc 10000 Hz Gain {profile.TrebleGainDb.ToString("F1", inv)} dB");
        }

        // Parametric Filter Nodes
        if (profile.ParametricFilters != null && profile.ParametricFilters.Count > 0)
        {
            var activeFilters = profile.ParametricFilters.Where(f => f.IsEnabled).ToList();
            if (activeFilters.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("# Parametric Filter Nodes");
                foreach (var filter in activeFilters)
                {
                    string fStr = filter.Frequency.ToString("F0", inv);
                    string gStr = filter.GainDb.ToString("F1", inv);
                    string qStr = filter.Q.ToString("F2", inv);

                    switch (filter.Type)
                    {
                        case ParametricFilterType.Peaking:
                            sb.AppendLine($"Filter: ON PK Fc {fStr} Hz Gain {gStr} dB Q {qStr}");
                            break;
                        case ParametricFilterType.LowShelf:
                            sb.AppendLine($"Filter: ON LSC Fc {fStr} Hz Gain {gStr} dB");
                            break;
                        case ParametricFilterType.HighShelf:
                            sb.AppendLine($"Filter: ON HSC Fc {fStr} Hz Gain {gStr} dB");
                            break;
                        case ParametricFilterType.Notch:
                            sb.AppendLine($"Filter: ON NO Fc {fStr} Hz Q {qStr}");
                            break;
                        case ParametricFilterType.HighPass:
                            sb.AppendLine($"Filter: ON HP Fc {fStr} Hz");
                            break;
                        case ParametricFilterType.LowPass:
                            sb.AppendLine($"Filter: ON LP Fc {fStr} Hz");
                            break;
                    }
                }
            }
        }

        // Stereo Widening Matrix
        if (profile.IsStereoWideningEnabled && profile.StereoWidthPercent != 100)
        {
            double w = Math.Clamp(profile.StereoWidthPercent, 0, 200) / 100.0;
            double coeffM = 0.5 * (1.0 + w);
            double coeffS = 0.5 * (1.0 - w);

            sb.AppendLine();
            sb.AppendLine($"# Stereo Widening Soundstage: {profile.StereoWidthPercent}%");
            sb.AppendLine($"Copy: L={coeffM.ToString("F3", inv)}*L+{coeffS.ToString("F3", inv)}*R R={coeffS.ToString("F3", inv)}*L+{coeffM.ToString("F3", inv)}*R");
        }

        // Dynamics Compressor annotation
        if (profile.IsCompressorEnabled)
        {
            sb.AppendLine();
            sb.AppendLine($"# Dynamics Compressor: Threshold {profile.CompressorThresholdDb.ToString("F1", inv)} dB, Ratio {profile.CompressorRatio.ToString("F1", inv)}:1");
        }

        return sb.ToString();
    }

    public Result ValidateConfigSyntax(string configContent)
    {
        if (string.IsNullOrWhiteSpace(configContent))
        {
            return Result.Failure("Config content is empty.");
        }

        using var reader = new StringReader(configContent);
        string? line;
        int lineNum = 0;

        while ((line = reader.ReadLine()) != null)
        {
            lineNum++;
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;

            bool isValid = false;

            if (trimmed.StartsWith("Device:", StringComparison.OrdinalIgnoreCase))
            {
                isValid = trimmed.Length > 8;
            }
            else if (trimmed.StartsWith("Preamp:", StringComparison.OrdinalIgnoreCase))
            {
                isValid = Regex.IsMatch(trimmed, @"^Preamp:\s*[-+]?[0-9]*\.?[0-9]+\s*dB$", RegexOptions.IgnoreCase);
            }
            else if (trimmed.StartsWith("GraphicEQ:", StringComparison.OrdinalIgnoreCase))
            {
                isValid = Regex.IsMatch(trimmed, @"^GraphicEQ:\s*([0-9]+\s+[-+]?[0-9]*\.?[0-9]+(\s*;\s*)?)+$", RegexOptions.IgnoreCase);
            }
            else if (trimmed.StartsWith("Filter:", StringComparison.OrdinalIgnoreCase))
            {
                isValid = Regex.IsMatch(trimmed, @"^Filter:\s*(ON|OFF)\s+[A-Za-z]+\s+Fc\s+[0-9]+(\.[0-9]+)?\s*Hz(\s+Gain\s+[-+]?[0-9]*\.?[0-9]+\s*dB)?(\s+Q\s+[0-9]*\.?[0-9]+)?$", RegexOptions.IgnoreCase);
            }
            else if (trimmed.StartsWith("Copy:", StringComparison.OrdinalIgnoreCase))
            {
                isValid = trimmed.Contains("L=") && trimmed.Contains("R=");
            }
            else if (trimmed.StartsWith("Include:", StringComparison.OrdinalIgnoreCase))
            {
                isValid = trimmed.Length > 9;
            }

            if (!isValid)
            {
                return Result.Failure($"Syntax error at line {lineNum}: '{trimmed}' is not a valid Equalizer APO directive.");
            }
        }

        return Result.Success();
    }

    public async Task<Result> ApplyProfileAsync(AudioProfile profile, string? targetDeviceName = null)
    {
        var status = CheckStatus();
        if (!status.IsInstalled || string.IsNullOrEmpty(status.InstallPath))
        {
            return Result.Failure("Equalizer APO is not installed on this system.");
        }

        try
        {
            string configDir = Path.Combine(status.InstallPath, "config");
            if (!Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
            }

            // 1. Generate & Validate configuration
            string configContent = GenerateConfigString(profile, targetDeviceName);
            var validation = ValidateConfigSyntax(configContent);
            if (!validation.IsSuccess)
            {
                return Result.Failure($"Configuration validation failed: {validation.Error}");
            }

            // 2. Backup master config if not already backed up
            await BackupConfigurationAsync();

            // 3. Write PRISM dedicated config safely
            string prismConfigPath = Path.Combine(configDir, PRISM_INCLUDE_FILE);
            try
            {
                await File.WriteAllTextAsync(prismConfigPath, configContent, Encoding.UTF8);
            }
            catch
            {
                string tmpConfigPath = prismConfigPath + ".tmp";
                await File.WriteAllTextAsync(tmpConfigPath, configContent, Encoding.UTF8);
                File.Copy(tmpConfigPath, prismConfigPath, true);
                try { File.Delete(tmpConfigPath); } catch { }
            }

            // 4. Ensure master config.txt includes prism_dsp.txt
            string masterPath = Path.Combine(configDir, MASTER_CONFIG_FILE);
            string includeLine = $"Include: {PRISM_INCLUDE_FILE}";
            
            bool alreadyIncluded = false;
            if (File.Exists(masterPath))
            {
                string existingMaster = await File.ReadAllTextAsync(masterPath);
                alreadyIncluded = existingMaster.Contains(PRISM_INCLUDE_FILE);
            }

            if (!alreadyIncluded)
            {
                await File.AppendAllTextAsync(masterPath, Environment.NewLine + includeLine + Environment.NewLine, Encoding.UTF8);
            }

            return Result.Success();
        }
        catch (UnauthorizedAccessException)
        {
            return Result.Failure("Administrative permissions required to update Equalizer APO config in Program Files. Please run PRISM with necessary privileges.");
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to apply Equalizer APO profile: {ex.Message}");
        }
    }

    public async Task<Result<string>> ExportConfigAsync(AudioProfile profile, string destinationPath)
    {
        try
        {
            string configContent = GenerateConfigString(profile);
            string dir = Path.GetDirectoryName(destinationPath) ?? "";
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            await File.WriteAllTextAsync(destinationPath, configContent, Encoding.UTF8);
            return Result<string>.Success(destinationPath);
        }
        catch (Exception ex)
        {
            return Result<string>.Failure($"Export failed: {ex.Message}");
        }
    }

    public async Task<Result> BackupConfigurationAsync()
    {
        var status = CheckStatus();
        if (!status.IsInstalled || string.IsNullOrEmpty(status.InstallPath))
            return Result.Success();

        try
        {
            string masterPath = Path.Combine(status.InstallPath, "config", MASTER_CONFIG_FILE);
            string backupPath = masterPath + ".prism_bak";

            if (File.Exists(masterPath) && !File.Exists(backupPath))
            {
                await Task.Run(() => File.Copy(masterPath, backupPath, false));
            }
            return Result.Success();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"APO Backup Exception: {ex.Message}");
            return Result.Failure(ex.Message);
        }
    }

    public async Task<Result> RestoreBackupConfigurationAsync()
    {
        var status = CheckStatus();
        if (!status.IsInstalled || string.IsNullOrEmpty(status.InstallPath))
            return Result.Success();

        try
        {
            string masterPath = Path.Combine(status.InstallPath, "config", MASTER_CONFIG_FILE);
            string backupPath = masterPath + ".prism_bak";

            if (File.Exists(backupPath))
            {
                await Task.Run(() => File.Copy(backupPath, masterPath, true));
                return Result.Success();
            }
            return Result.Failure("No PRISM backup found to restore.");
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }
}
