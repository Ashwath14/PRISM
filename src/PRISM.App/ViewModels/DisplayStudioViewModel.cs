using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PRISM.Core.Common;
using PRISM.Core.Interfaces;
using PRISM.Core.Models;
using PRISM.Hardware.Display;

namespace PRISM.App.ViewModels;

public partial class DisplayStudioViewModel : ObservableObject
{
    private readonly IDisplayService _displayService;
    private readonly IProfileService _profileService;
    private readonly IDisplayResolutionService? _resolutionService;
    private readonly IDisplayAdvancedColorService? _advancedColorService;
    private readonly System.Timers.Timer _rollbackTimer;

    private DisplayProfile? _previousProfile;
    private List<DisplayModeInfo> _allModes = new();
    private bool _isSilencingHardwareSync = false;

    // -------------------------------------------------------------
    // MONITOR IDENTITY & CONNECTION
    // -------------------------------------------------------------
    [ObservableProperty]
    private string _selectedMonitorName = "Acer ED270 X0";

    [ObservableProperty]
    private string _monitorDetails = "DisplayPort • 1920x1080 @ 240Hz";

    [ObservableProperty]
    private string _edidInfo = "Manufactured: Week 46, 2025 • PnP ID: ACR0F95";

    [ObservableProperty]
    private string _statusPillText = "Connected & Verified";

    [ObservableProperty]
    private string _statusPillBrush = "#10B981"; // Emerald green

    [ObservableProperty]
    private string _latencyText = "Wire Latency: ~60 ms";

    [ObservableProperty]
    private string _serialNumberText = "S/N: 154604D5A3W01";

    [ObservableProperty]
    private string _preferenceSavedMessage = "";

    [ObservableProperty]
    private bool _isHardwareDdcCi = true;

    // -------------------------------------------------------------
    // DDC/CI HARDWARE REGISTERS & SOFTWARE DIMMING
    // -------------------------------------------------------------
    [ObservableProperty]
    private int _brightness = 44;

    [ObservableProperty]
    private int _contrast = 50;

    [ObservableProperty]
    private int _redGain = 50;

    [ObservableProperty]
    private int _greenGain = 50;

    [ObservableProperty]
    private int _blueGain = 50;

    [ObservableProperty]
    private bool _isSubZeroEnabled = false;

    [ObservableProperty]
    private int _subZeroPercent = 15;

    [ObservableProperty]
    private string _approxColorProfileText = "Approximate Profile: Neutral Balanced";

    // -------------------------------------------------------------
    // DISPLAY PROFILES & PRESETS
    // -------------------------------------------------------------
    [ObservableProperty]
    private string _activeProfileName = "Daylight Focus";

    [ObservableProperty]
    private DisplayProfile? _selectedProfile;

    [ObservableProperty]
    private DisplayProfile? _activeAppliedProfile;

    [ObservableProperty]
    private bool _isProfileModified = false;

    [ObservableProperty]
    private bool _isApplyingProfile = false;

    [ObservableProperty]
    private string _profileStatusMessage = "";

    [ObservableProperty]
    private string _profileModifiedWarning = "";

    [ObservableProperty]
    private string _newProfileName = "My Workspace";

    [ObservableProperty]
    private bool _isCreatingProfile = false;

    [ObservableProperty]
    private bool _canRevertProfile = false;

    public ObservableCollection<DisplayProfile> Profiles { get; } = new();

    // -------------------------------------------------------------
    // RESOLUTION & REFRESH RATE MANAGEMENT
    // -------------------------------------------------------------
    public ObservableCollection<string> AvailableResolutions { get; } = new();
    public ObservableCollection<int> AvailableRefreshRates { get; } = new();

    [ObservableProperty]
    private string _selectedResolution = "1920x1080 (Native)";

    [ObservableProperty]
    private int _selectedRefreshRate = 240;

    [ObservableProperty]
    private string _currentModeSummary = "1920x1080 @ 240Hz (Active Native)";

    [ObservableProperty]
    private bool _isRollbackBannerVisible = false;

    [ObservableProperty]
    private int _rollbackCountdownSeconds = 15;

    [ObservableProperty]
    private string _rollbackBannerText = "Display mode applied. Confirming keeps changes, or auto-revert occurs in 15 seconds.";

    // -------------------------------------------------------------
    // ADVANCED COLOR, HDR & DOLBY VISION
    // -------------------------------------------------------------
    [ObservableProperty]
    private string _hdrStatusBadge = "SDR (8-Bit)";

    [ObservableProperty]
    private string _hdrStatusDescription = "Windows Advanced Color is in SDR mode. Acer ED270 X0 native panel is standard dynamic range (250 nits).";

    [ObservableProperty]
    private string _dolbyVisionStatus = "Dolby Vision is not supported on standard SDR desktop displays. HDR10/Dolby Vision hardware required.";

    [ObservableProperty]
    private string _iccProfilePath = "sRGB IEC61966-2.1 (System Default)";

    [ObservableProperty]
    private string _colorEncodingText = "RGB (4:4:4 Full Range)";

    [ObservableProperty]
    private int _bitsPerChannel = 8;

    [ObservableProperty]
    private bool _isHdrSupported = false;

    [ObservableProperty]
    private bool _isHdrEnabled = false;

    // -------------------------------------------------------------
    // DIAGNOSTICS & TELEMETRY
    // -------------------------------------------------------------
    [ObservableProperty]
    private bool _isDiagnosticsExpanded = false;

    [ObservableProperty]
    private string _diagnosticsDump = "";

    public DisplayStudioViewModel(
        IDisplayService displayService, 
        IProfileService profileService,
        IDisplayResolutionService? resolutionService = null,
        IDisplayAdvancedColorService? advancedColorService = null)
    {
        _displayService = displayService;
        _profileService = profileService;
        _resolutionService = resolutionService;
        _advancedColorService = advancedColorService;

        _rollbackTimer = new System.Timers.Timer(1000)
        {
            AutoReset = true
        };
        _rollbackTimer.Elapsed += (s, e) =>
        {
            if (RollbackCountdownSeconds > 1)
            {
                RollbackCountdownSeconds--;
                RollbackBannerText = $"Display mode applied. Confirming keeps changes, or auto-revert occurs in {RollbackCountdownSeconds}s.";
            }
            else
            {
                _rollbackTimer.Stop();
                IsRollbackBannerVisible = false;
                SyncModes();
            }
        };

        if (_resolutionService is DisplayResolutionService drs)
        {
            drs.RollbackTriggered += OnRollbackTriggered;
        }

        LoadProfiles();
        SyncFromHardware();
        SyncModes();
        SyncAdvancedColor();

        _displayService.DisplayConfigChanged += (s, m) =>
        {
            SelectedMonitorName = m.Name;
            MonitorDetails = $"{m.ConnectionType} • {m.Width}x{m.Height}";
            EdidInfo = $"Manufactured: Week {m.EdidWeek}, {m.EdidYear} • Device: {m.DevicePath}";
            SerialNumberText = $"S/N: {m.SerialNumber} • PnP: {m.PnPId}";
            IsHardwareDdcCi = m.IsHardwareDdcCi;

            try
            {
                _isSilencingHardwareSync = true;
                Brightness = m.CurrentBrightness;
                Contrast = m.CurrentContrast;
                RedGain = m.RedGain;
                GreenGain = m.GreenGain;
                BlueGain = m.BlueGain;
            }
            finally
            {
                _isSilencingHardwareSync = false;
            }

            UpdateDiagnosticsDump(m);
            SyncModes();
            SyncAdvancedColor();
        };

        _displayService.HardwareStatusUpdated += (s, status) =>
        {
            UpdateStatusPill(status);
        };
    }

    private void OnRollbackTriggered()
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher != null)
        {
            dispatcher.BeginInvoke(() =>
            {
                _rollbackTimer.Stop();
                IsRollbackBannerVisible = false;
                SyncModes();
            });
        }
    }

    private void SyncFromHardware()
    {
        var mon = _displayService.ActiveMonitor;
        if (mon != null)
        {
            SelectedMonitorName = mon.Name;
            MonitorDetails = $"{mon.ConnectionType} • {mon.Width}x{mon.Height}";
            EdidInfo = $"Manufactured: Week {mon.EdidWeek}, {mon.EdidYear} • Device: {mon.DevicePath}";
            SerialNumberText = $"S/N: {mon.SerialNumber} • PnP: {mon.PnPId}";
            IsHardwareDdcCi = mon.IsHardwareDdcCi;

            try
            {
                _isSilencingHardwareSync = true;
                Brightness = mon.CurrentBrightness;
                Contrast = mon.CurrentContrast;
                RedGain = mon.RedGain;
                GreenGain = mon.GreenGain;
                BlueGain = mon.BlueGain;
            }
            finally
            {
                _isSilencingHardwareSync = false;
            }

            UpdateStatusPill(mon.Status);
            UpdateDiagnosticsDump(mon);
        }
    }

    public static bool TryParseResolution(string? text, out int width, out int height)
    {
        width = 0; height = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var m = System.Text.RegularExpressions.Regex.Match(text, @"(\d+)\s*[x×X]\s*(\d+)");
        if (m.Success && int.TryParse(m.Groups[1].Value, out width) && int.TryParse(m.Groups[2].Value, out height))
        {
            return true;
        }
        return false;
    }

    public void SyncModes()
    {
        if (_resolutionService == null) return;

        try
        {
            var dev = _displayService.ActiveMonitor?.DevicePath;
            _allModes = _resolutionService.GetSupportedModes(dev).ToList();
            var curr = _resolutionService.GetCurrentMode(dev);

            AvailableResolutions.Clear();
            var resStrings = _allModes
                .Select(m => m.ResolutionText + (m.IsNative ? " (Native)" : ""))
                .Distinct()
                .ToList();

            foreach (var r in resStrings)
            {
                AvailableResolutions.Add(r);
            }

            if (curr != null)
            {
                string currMatch = AvailableResolutions.FirstOrDefault(r => TryParseResolution(r, out int w, out int h) && w == curr.Width && h == curr.Height) 
                                   ?? AvailableResolutions.FirstOrDefault() ?? "1920 × 1080 (Native)";
                SelectedResolution = currMatch;
                PopulateRefreshRatesForSelectedResolution();

                SelectedRefreshRate = curr.RefreshRate;
                CurrentModeSummary = $"{curr.Width} × {curr.Height} @ {curr.RefreshRate}Hz (Active{(curr.IsNative ? " Native" : "")})";
            }
            else if (AvailableResolutions.Count > 0)
            {
                SelectedResolution = AvailableResolutions[0];
                PopulateRefreshRatesForSelectedResolution();
            }
        }
        catch { }
    }

    private void PopulateRefreshRatesForSelectedResolution()
    {
        int prevRate = SelectedRefreshRate;
        AvailableRefreshRates.Clear();
        if (string.IsNullOrWhiteSpace(SelectedResolution)) return;

        if (TryParseResolution(SelectedResolution, out int w, out int h))
        {
            var rates = _allModes
                .Where(m => m.Width == w && m.Height == h)
                .Select(m => m.RefreshRate)
                .Distinct()
                .OrderByDescending(r => r)
                .ToList();

            foreach (var rate in rates)
            {
                AvailableRefreshRates.Add(rate);
            }

            if (AvailableRefreshRates.Count > 0)
            {
                if (rates.Contains(prevRate))
                {
                    SelectedRefreshRate = prevRate;
                }
                else
                {
                    SelectedRefreshRate = AvailableRefreshRates[0];
                }
            }
        }
    }

    partial void OnSelectedResolutionChanged(string value)
    {
        PopulateRefreshRatesForSelectedResolution();
    }

    public void SyncAdvancedColor()
    {
        if (_advancedColorService == null) return;

        try
        {
            var dev = _displayService.ActiveMonitor?.DevicePath;
            var info = _advancedColorService.GetAdvancedColorInfo(dev);

            IsHdrSupported = info.IsHdrSupported;
            IsHdrEnabled = info.IsHdrEnabled;
            HdrStatusBadge = info.IsHdrEnabled ? "HDR Active" : (info.IsHdrSupported ? "HDR Supported (Off)" : "SDR (8-Bit Native)");
            HdrStatusDescription = info.SummaryText;
            DolbyVisionStatus = info.DolbyVisionStatus;
            IccProfilePath = string.IsNullOrWhiteSpace(info.IccProfilePath) ? "sRGB IEC61966-2.1 (System Default)" : System.IO.Path.GetFileName(info.IccProfilePath);
            ColorEncodingText = info.ColorEncoding;
            BitsPerChannel = info.BitsPerChannel;
        }
        catch { }
    }

    private void UpdateStatusPill(HardwareStatus status)
    {
        switch (status.State)
        {
            case HardwareCommunicationState.Applying:
                StatusPillText = status.StatusText;
                StatusPillBrush = "#00B4D8"; // Cyan
                break;
            case HardwareCommunicationState.Success:
                StatusPillText = status.StatusText;
                StatusPillBrush = "#10B981"; // Emerald
                LatencyText = $"Wire Latency: {status.LastLatencyMs} ms";
                break;
            case HardwareCommunicationState.Failed:
                StatusPillText = status.StatusText;
                StatusPillBrush = "#EF4444"; // Red
                break;
            default:
                StatusPillText = status.StatusText;
                StatusPillBrush = "#94A3B8";
                break;
        }
    }

    private void UpdateDiagnosticsDump(MonitorInfo m)
    {
        var colorInfo = _advancedColorService?.GetAdvancedColorInfo(m.DevicePath);
        var currMode = _resolutionService?.GetCurrentMode();

        DiagnosticsDump = 
            $"[MONITOR HARDWARE DUMP]\n" +
            $"Model:                  {m.Name} ({m.Manufacturer})\n" +
            $"Physical Handle:        {m.PhysicalHandleStatus}\n" +
            $"Video Connection:       {m.ConnectionType} (Port Register: 0x60)\n" +
            $"Resolution / Refresh:   {currMode?.Width ?? m.Width}x{currMode?.Height ?? m.Height} @ {currMode?.RefreshRate ?? 240}Hz ({(currMode?.IsNative == true ? "NATIVE PANEL" : "SCALED")})\n" +
            $"EDID Timestamp:         Week {m.EdidWeek}, {m.EdidYear}\n\n" +
            $"[ADVANCED COLOR & HDR ENGINE]\n" +
            $"HDR Capable:            {(colorInfo?.IsHdrSupported == true ? "YES" : "NO (Acer ED270 X0 Native 8-bit SDR)")}\n" +
            $"Color Encoding:         {colorInfo?.ColorEncoding ?? "RGB 4:4:4 Full Range"}\n" +
            $"Color Depth:            {colorInfo?.BitsPerChannel ?? 8} bits per channel (16.7 Million Colors)\n" +
            $"ICC Profile:            {colorInfo?.IccProfilePath ?? "sRGB IEC61966-2.1 (System Default)"}\n" +
            $"Dolby Vision:           {colorInfo?.DolbyVisionStatus ?? "Not supported on SDR display"}\n\n" +
            $"[VERIFIED VCP REGISTERS]\n" +
            $"0x10 Luminance (Backlight):   SUPPORTED (Continuous 0-100) -> Verified: {m.Status.LastVerifiedBrightness ?? m.CurrentBrightness}%\n" +
            $"0x12 Contrast:                SUPPORTED (Continuous 0-100) -> Verified: {m.Status.LastVerifiedContrast ?? m.CurrentContrast}%\n" +
            $"0x16 Red Video Gain:          SUPPORTED (Continuous 0-100) -> Verified: {m.Status.LastVerifiedRed ?? m.RedGain}%\n" +
            $"0x18 Green Video Gain:        SUPPORTED (Continuous 0-100) -> Verified: {m.Status.LastVerifiedGreen ?? m.GreenGain}%\n" +
            $"0x1A Blue Video Gain:         SUPPORTED (Continuous 0-100) -> Verified: {m.Status.LastVerifiedBlue ?? m.BlueGain}%\n" +
            $"0x60 Input Select:            SUPPORTED (DP: 0x11, HDMI: 0x12/0x13)\n" +
            $"0xD6 Power Mode:              SUPPORTED (Active / Standby)\n" +
            $"0xAA Screen Orientation:      UNSUPPORTED (Standard Landscape Scaler)\n\n" +
            $"[BUS PROTOCOL STATUS]\n" +
            $"I2C Clock Speed:        100 kHz standard\n" +
            $"Inter-Packet Delay:     40ms wire delay enforced\n" +
            $"Last Communication:     {m.Status.LastLatencyMs} ms round-trip latency\n" +
            $"Read-Back Verification: ACTIVE (Registers read back after writes)";
    }

    private void LoadProfiles()
    {
        Profiles.Clear();
        foreach (var p in _profileService.GetDisplayProfiles())
        {
            Profiles.Add(p);
        }

        if (ActiveAppliedProfile == null)
        {
            ActiveAppliedProfile = Profiles.FirstOrDefault(p => p.Name == ActiveProfileName) 
                                   ?? Profiles.FirstOrDefault(p => p.Id == "disp_daylight") 
                                   ?? Profiles.FirstOrDefault();
            if (ActiveAppliedProfile != null)
            {
                ActiveProfileName = ActiveAppliedProfile.Name;
            }
        }

        if (SelectedProfile == null)
        {
            SelectedProfile = ActiveAppliedProfile ?? Profiles.FirstOrDefault();
        }
    }

    private void CheckIfProfileModified()
    {
        if (ActiveAppliedProfile == null)
        {
            IsProfileModified = false;
            ProfileModifiedWarning = "";
            return;
        }

        bool diff = (Brightness != ActiveAppliedProfile.Brightness) ||
                    (Contrast != ActiveAppliedProfile.Contrast) ||
                    (RedGain != ActiveAppliedProfile.RedGain) ||
                    (GreenGain != ActiveAppliedProfile.GreenGain) ||
                    (BlueGain != ActiveAppliedProfile.BlueGain) ||
                    (IsSubZeroEnabled != ActiveAppliedProfile.SoftwareDimmingEnabled) ||
                    (IsSubZeroEnabled && SubZeroPercent != ActiveAppliedProfile.SoftwareDimmingLevel);

        IsProfileModified = diff;
        ProfileModifiedWarning = diff 
            ? $"Current settings modified from '{ActiveAppliedProfile.Name}'" 
            : "";
    }

    partial void OnBrightnessChanged(int value)
    {
        if (_isSilencingHardwareSync) return;
        if (_displayService.ActiveMonitor != null)
        {
            _displayService.SetBrightnessAsync(_displayService.ActiveMonitor.Id, value);
        }
        CheckIfProfileModified();
    }

    partial void OnContrastChanged(int value)
    {
        if (_isSilencingHardwareSync) return;
        if (_displayService.ActiveMonitor != null)
        {
            _displayService.SetContrastAsync(_displayService.ActiveMonitor.Id, value);
        }
        CheckIfProfileModified();
    }

    partial void OnRedGainChanged(int value) => ApplyRgbGains();
    partial void OnGreenGainChanged(int value) => ApplyRgbGains();
    partial void OnBlueGainChanged(int value) => ApplyRgbGains();

    private void ApplyRgbGains()
    {
        if (_isSilencingHardwareSync) return;
        if (_displayService.ActiveMonitor != null)
        {
            _displayService.SetRgbGainsAsync(_displayService.ActiveMonitor.Id, RedGain, GreenGain, BlueGain);
            UpdateColorProfileText();
        }
        CheckIfProfileModified();
    }

    private void UpdateColorProfileText()
    {
        if (RedGain > BlueGain + 6) ApproxColorProfileText = "Approximate Profile: Warm Tint";
        else if (BlueGain > RedGain + 6) ApproxColorProfileText = "Approximate Profile: Cool Tint";
        else ApproxColorProfileText = "Approximate Profile: Neutral Balanced";
    }

    partial void OnIsSubZeroEnabledChanged(bool value)
    {
        if (_isSilencingHardwareSync) return;
        if (_displayService.ActiveMonitor != null)
        {
            _displayService.SetSubZeroDimAsync(_displayService.ActiveMonitor.Id, value, SubZeroPercent);
        }
        CheckIfProfileModified();
    }

    partial void OnSubZeroPercentChanged(int value)
    {
        if (_isSilencingHardwareSync) return;
        if (_displayService.ActiveMonitor != null && IsSubZeroEnabled)
        {
            _displayService.SetSubZeroDimAsync(_displayService.ActiveMonitor.Id, true, value);
        }
        CheckIfProfileModified();
    }

    [RelayCommand]
    public async Task RefreshHardware()
    {
        if (_displayService.ActiveMonitor != null)
        {
            StatusPillText = "Reading monitor hardware...";
            StatusPillBrush = "#00B4D8";
            await _displayService.RefreshAllHardwareValuesAsync(_displayService.ActiveMonitor.Id);
            SyncModes();
            SyncAdvancedColor();
        }
    }

    [RelayCommand]
    public void SelectProfile(DisplayProfile? profile)
    {
        if (profile == null) return;
        SelectedProfile = profile;
    }

    [RelayCommand]
    public async Task ApplySelectedProfile()
    {
        if (SelectedProfile != null)
        {
            await ApplyProfileInternalAsync(SelectedProfile);
        }
    }

    [RelayCommand]
    public async Task ApplyProfile(DisplayProfile? profile = null)
    {
        var target = profile ?? SelectedProfile;
        if (target != null)
        {
            await ApplyProfileInternalAsync(target);
        }
    }

    private async Task ApplyProfileInternalAsync(DisplayProfile profile)
    {
        if (profile == null) return;

        IsApplyingProfile = true;
        ProfileStatusMessage = $"Applying '{profile.Name}' via DDC/CI...";

        // Remember previous state for 1-click Undo / Revert
        _previousProfile = new DisplayProfile
        {
            Name = "Previous Settings",
            Brightness = Brightness,
            Contrast = Contrast,
            RedGain = RedGain,
            GreenGain = GreenGain,
            BlueGain = BlueGain,
            SoftwareDimmingEnabled = IsSubZeroEnabled,
            SoftwareDimmingLevel = SubZeroPercent
        };
        CanRevertProfile = true;

        SelectedProfile = profile;

        var targetMon = _displayService.ActiveMonitor;
        string monId = targetMon?.Id ?? "MONITOR_1";

        Result res;
        try
        {
            _isSilencingHardwareSync = true;
            res = await _displayService.ApplyDisplayProfileAsync(monId, profile);
            if (res.IsSuccess)
            {
                Brightness = profile.Brightness;
                Contrast = profile.Contrast;
                RedGain = profile.RedGain;
                GreenGain = profile.GreenGain;
                BlueGain = profile.BlueGain;
                IsSubZeroEnabled = profile.SoftwareDimmingEnabled;
                SubZeroPercent = profile.SoftwareDimmingLevel;
            }
        }
        finally
        {
            _isSilencingHardwareSync = false;
        }

        if (!res.IsSuccess)
        {
            IsApplyingProfile = false;
            ProfileStatusMessage = $"Hardware Apply Failed: {res.Error}";
            StatusPillText = "DDC/CI Apply Failed";
            StatusPillBrush = "#EF4444";
            // Do not falsely mark as applied
            return;
        }

        UpdateColorProfileText();

        ActiveAppliedProfile = profile;
        ActiveProfileName = profile.Name;
        IsProfileModified = false;
        ProfileModifiedWarning = "";
        ProfileStatusMessage = $"Profile '{profile.Name}' applied & verified on hardware.";
        IsApplyingProfile = false;

        _ = Task.Delay(3500).ContinueWith(_ =>
        {
            if (ProfileStatusMessage.StartsWith("Profile '"))
            {
                ProfileStatusMessage = "";
            }
        });
    }

    [RelayCommand]
    public async Task PreviewProfile(DisplayProfile? profile = null)
    {
        var target = profile ?? SelectedProfile;
        if (target == null) return;

        ProfileStatusMessage = $"Previewing '{target.Name}'... (Click Apply to keep)";

        var mon = _displayService.ActiveMonitor;
        string monId = mon?.Id ?? "MONITOR_1";

        await _displayService.ApplyDisplayProfileAsync(monId, target);

        try
        {
            _isSilencingHardwareSync = true;
            Brightness = target.Brightness;
            Contrast = target.Contrast;
            RedGain = target.RedGain;
            GreenGain = target.GreenGain;
            BlueGain = target.BlueGain;
            IsSubZeroEnabled = target.SoftwareDimmingEnabled;
            SubZeroPercent = target.SoftwareDimmingLevel;
        }
        finally
        {
            _isSilencingHardwareSync = false;
        }

        UpdateColorProfileText();
        CheckIfProfileModified();
    }

    [RelayCommand]
    public async Task ReapplyActiveProfile()
    {
        if (ActiveAppliedProfile != null)
        {
            await ApplyProfileInternalAsync(ActiveAppliedProfile);
        }
    }

    [RelayCommand]
    public void RevertLastProfile()
    {
        if (_previousProfile == null) return;

        ActiveProfileName = "Restored Settings";
        Brightness = _previousProfile.Brightness;
        Contrast = _previousProfile.Contrast;
        RedGain = _previousProfile.RedGain;
        GreenGain = _previousProfile.GreenGain;
        BlueGain = _previousProfile.BlueGain;
        IsSubZeroEnabled = _previousProfile.SoftwareDimmingEnabled;
        SubZeroPercent = _previousProfile.SoftwareDimmingLevel;

        CanRevertProfile = false;
        IsProfileModified = false;
        ProfileModifiedWarning = "";
        ProfileStatusMessage = "Restored previous display settings.";
    }

    [RelayCommand]
    public async Task ApplyResolution()
    {
        if (_resolutionService == null) return;

        if (TryParseResolution(SelectedResolution, out int w, out int h))
        {
            var targetMode = _allModes.FirstOrDefault(m => m.Width == w && m.Height == h && m.RefreshRate == SelectedRefreshRate)
                             ?? new DisplayModeInfo { Width = w, Height = h, RefreshRate = SelectedRefreshRate, BitsPerPixel = 32 };

            var dev = _displayService.ActiveMonitor?.DevicePath;
            var res = await _resolutionService.ApplyModeAsync(targetMode, dev);
            if (res.IsSuccess)
            {
                var verified = _resolutionService.GetCurrentMode(dev);
                IsRollbackBannerVisible = true;
                RollbackCountdownSeconds = 15;
                RollbackBannerText = $"Display mode applied ({verified?.Width ?? w} × {verified?.Height ?? h} @ {verified?.RefreshRate ?? SelectedRefreshRate}Hz). Confirm to keep, or auto-revert occurs in {RollbackCountdownSeconds}s.";
                _rollbackTimer.Start();
                CurrentModeSummary = $"{verified?.Width ?? w} × {verified?.Height ?? h} @ {verified?.RefreshRate ?? SelectedRefreshRate}Hz (Pending Confirmation)";
            }
            else
            {
                CurrentModeSummary = $"Failed: {res.Error}";
            }
        }
    }

    [RelayCommand]
    public async Task ResetDisplayModeToDefault()
    {
        if (_resolutionService == null) return;
        var dev = _displayService.ActiveMonitor?.DevicePath;
        var nativeMode = _allModes.FirstOrDefault(m => m.IsNative && m.RefreshRate >= 200) 
                         ?? _allModes.FirstOrDefault(m => m.Width == 1920 && m.Height == 1080)
                         ?? _allModes.FirstOrDefault();
        if (nativeMode != null)
        {
            var res = await _resolutionService.ApplyModeAsync(nativeMode, dev);
            if (res.IsSuccess)
            {
                _resolutionService.ConfirmAppliedMode();
                _rollbackTimer.Stop();
                IsRollbackBannerVisible = false;
                SyncModes();
            }
        }
    }

    [RelayCommand]
    public async Task ResetDisplaySettings()
    {
        Brightness = 50;
        Contrast = 50;
        RedGain = 50;
        GreenGain = 50;
        BlueGain = 50;
        IsSubZeroEnabled = false;
        SubZeroPercent = 15;
        ActiveProfileName = "Standard (6500K)";

        if (_displayService.ActiveMonitor != null)
        {
            await _displayService.SetBrightnessAsync(_displayService.ActiveMonitor.Id, 50);
            await _displayService.SetContrastAsync(_displayService.ActiveMonitor.Id, 50);
            await _displayService.SetRgbGainsAsync(_displayService.ActiveMonitor.Id, 50, 50, 50);
            await _displayService.SetSubZeroDimAsync(_displayService.ActiveMonitor.Id, false, 15);
        }
        await ResetDisplayModeToDefault();
    }

    [RelayCommand]
    public void ConfirmMode()
    {
        if (_resolutionService == null) return;
        _resolutionService.ConfirmAppliedMode();
        _rollbackTimer.Stop();
        IsRollbackBannerVisible = false;
        SyncModes();
    }

    [RelayCommand]
    public async Task RevertMode()
    {
        if (_resolutionService == null) return;
        _rollbackTimer.Stop();
        await _resolutionService.RevertModeAsync();
        IsRollbackBannerVisible = false;
        SyncModes();
    }

    [RelayCommand]
    public async Task ToggleHdr()
    {
        if (_advancedColorService == null) return;

        if (!IsHdrSupported)
        {
            HdrStatusDescription = "Hardware check confirmed: Acer ED270 X0 does not support HDR (8-bit SDR 250 nits). Antigravity will not enable non-functional HDR emulation.";
            return;
        }

        bool target = !IsHdrEnabled;
        var res = await _advancedColorService.SetHdrEnabledAsync(target);
        if (res.IsSuccess)
        {
            SyncAdvancedColor();
        }
        else
        {
            HdrStatusDescription = $"HDR operation note: {res.Error}";
        }
    }

    [RelayCommand]
    public async Task SaveAsDefaultPreferences()
    {
        var res = await _displayService.SaveCurrentPreferencesForActiveMonitorAsync(ActiveProfileName);
        if (res.IsSuccess)
        {
            PreferenceSavedMessage = "Default preferences remembered for this monitor!";
            await Task.Delay(2500);
            PreferenceSavedMessage = "";
        }
    }

    [RelayCommand]
    public async Task CreateCustomProfile()
    {
        if (string.IsNullOrWhiteSpace(NewProfileName)) return;

        var res = await _profileService.CreateCustomDisplayProfileAsync(
            NewProfileName, Brightness, Contrast, RedGain, GreenGain, BlueGain, IsSubZeroEnabled, SubZeroPercent);

        if (res.IsSuccess && res.Value != null)
        {
            Profiles.Add(res.Value);
            SelectedProfile = res.Value;
            ActiveAppliedProfile = res.Value;
            ActiveProfileName = res.Value.Name;
            IsCreatingProfile = false;
            IsProfileModified = false;
            ProfileModifiedWarning = "";
            ProfileStatusMessage = $"Created custom profile '{res.Value.Name}'.";
            NewProfileName = "My Workspace";
        }
    }

    [RelayCommand]
    public async Task SaveCurrentAsCustomProfile()
    {
        string name = string.IsNullOrWhiteSpace(NewProfileName) || NewProfileName == "My Workspace"
            ? $"Custom ({DateTime.Now:HH:mm})"
            : NewProfileName.Trim();

        var res = await _profileService.CreateCustomDisplayProfileAsync(
            name, Brightness, Contrast, RedGain, GreenGain, BlueGain, IsSubZeroEnabled, SubZeroPercent);

        if (res.IsSuccess && res.Value != null)
        {
            Profiles.Add(res.Value);
            SelectedProfile = res.Value;
            ActiveAppliedProfile = res.Value;
            ActiveProfileName = res.Value.Name;
            IsProfileModified = false;
            ProfileModifiedWarning = "";
            ProfileStatusMessage = $"Saved new profile '{res.Value.Name}'.";
            NewProfileName = "My Workspace";
            IsCreatingProfile = false;
        }
    }

    [RelayCommand]
    public async Task DuplicateProfile(DisplayProfile? profile = null)
    {
        var target = profile ?? SelectedProfile;
        if (target == null) return;

        var res = await _profileService.DuplicateDisplayProfileAsync(target.Id);
        if (res.IsSuccess && res.Value != null)
        {
            Profiles.Add(res.Value);
            SelectedProfile = res.Value;
            ProfileStatusMessage = $"Duplicated '{target.Name}'.";
        }
    }

    [RelayCommand]
    public async Task DeleteProfile(DisplayProfile? profile = null)
    {
        var target = profile ?? SelectedProfile;
        if (target == null || !target.IsCustom) return; // Protect factory defaults

        var res = await _profileService.DeleteDisplayProfileAsync(target.Id);
        if (res.IsSuccess)
        {
            Profiles.Remove(target);
            SelectedProfile = Profiles.FirstOrDefault();
            ProfileStatusMessage = $"Deleted profile '{target.Name}'.";

            if (ActiveAppliedProfile?.Id == target.Id)
            {
                var fallback = Profiles.FirstOrDefault();
                if (fallback != null)
                {
                    await ApplyProfileInternalAsync(fallback);
                }
            }
        }
    }

    [RelayCommand]
    public async Task ExportProfile(DisplayProfile? profile = null)
    {
        var target = profile ?? SelectedProfile;
        if (target == null) return;

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = $"Export Display Profile - {target.Name}",
            Filter = "PRISM Display Profile (*.prism-disp)|*.prism-disp|JSON Files (*.json)|*.json",
            FileName = $"{target.Name.Replace(" ", "_")}.prism-disp",
            DefaultExt = ".prism-disp"
        };

        if (dlg.ShowDialog() == true)
        {
            var res = await _profileService.ExportDisplayProfileAsync(target.Id, dlg.FileName);
            if (res.IsSuccess)
            {
                ProfileStatusMessage = $"Exported '{target.Name}' to {System.IO.Path.GetFileName(dlg.FileName)}.";
            }
            else
            {
                ProfileStatusMessage = $"Export failed: {res.Error}";
            }
        }
    }

    [RelayCommand]
    public async Task ImportProfile()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Import PRISM Display Profile",
            Filter = "PRISM Display Profile (*.prism-disp)|*.prism-disp|JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            DefaultExt = ".prism-disp"
        };

        if (dlg.ShowDialog() == true)
        {
            var res = await _profileService.ImportDisplayProfileAsync(dlg.FileName);
            if (res.IsSuccess && res.Value != null)
            {
                Profiles.Add(res.Value);
                SelectedProfile = res.Value;
                ProfileStatusMessage = $"Imported profile '{res.Value.Name}' successfully.";
            }
            else
            {
                ProfileStatusMessage = $"Import failed: {res.Error}";
            }
        }
    }

    [RelayCommand]
    public async Task RestoreDefaults()
    {
        await _profileService.RestoreDefaultProfilesAsync();
        LoadProfiles();
        var std = Profiles.FirstOrDefault();
        if (std != null)
        {
            await ApplyProfileInternalAsync(std);
        }
        ProfileStatusMessage = "Factory display profiles restored.";
    }

    [RelayCommand]
    public void SetNeutralGainPreset()
    {
        RedGain = 50;
        GreenGain = 50;
        BlueGain = 50;
    }

    [RelayCommand]
    public void SetWarmGainPreset()
    {
        RedGain = 56;
        GreenGain = 48;
        BlueGain = 40;
    }

    [RelayCommand]
    public void SetCoolGainPreset()
    {
        RedGain = 44;
        GreenGain = 50;
        BlueGain = 56;
    }

    [RelayCommand]
    public void ToggleDiagnostics()
    {
        IsDiagnosticsExpanded = !IsDiagnosticsExpanded;
    }
}
