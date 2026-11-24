using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PRISM.Core.Common;
using PRISM.Core.Interfaces;
using PRISM.Core.Models;
using PRISM.Hardware.Audio;
using Path = System.IO.Path;

namespace PRISM.App.ViewModels;

public partial class AudioStudioViewModel : ObservableObject, IDisposable
{
    private readonly IAudioService _audioService;
    private readonly IEqualizerApoBridge _apoBridge;
    private readonly IProfileService _profileService;
    private readonly ISpatialAudioService? _spatialAudioService;
    private readonly WasapiLoopbackVisualizer _visualizer;
    private readonly PrismAudioEngine _audioEngine = new();
    private readonly AsyncDebouncer _apoDebouncer = new(100);

    // In-App Real-Time DSP Audition Player
    [ObservableProperty] private bool _isAuditionPlaying = false;
    [ObservableProperty] private string _auditionButtonText = "▶ Play Audition Track";
    [ObservableProperty] private AuditionTrack _selectedAuditionTrack = AuditionTrack.SynthGroove;
    [ObservableProperty] private string _auditionTrackName = "Dynamic Synth Groove (Kick, Bass & Synth)";
    [ObservableProperty] private string _auditionDspStatus = "Active (Real-Time 10-Band Biquad Engine)";
    [ObservableProperty] private string _systemApoStatus = "Equalizer APO: Not Installed (System audio unaffected until installed)";
    [ObservableProperty] private string _systemApoBadgeBrush = "#F59E0B";

    // Audio Output Device
    [ObservableProperty] private string _activeDeviceName = "Cosmic Byte Proteus";
    [ObservableProperty] private int _masterVolume = 75;
    [ObservableProperty] private bool _isMuted = false;

    // Left / Right Channel Balance
    [ObservableProperty] private float _channelBalanceLeft = 1.0f;
    [ObservableProperty] private float _channelBalanceRight = 1.0f;
    [ObservableProperty] private string _channelBalanceText = "Center (100% / 100%)";

    // 10-Band EQ Frequencies in dB (-12.0 to +12.0 dB)
    [ObservableProperty] private double _gain32Hz = 0.0;
    [ObservableProperty] private double _gain64Hz = 0.0;
    [ObservableProperty] private double _gain125Hz = 0.0;
    [ObservableProperty] private double _gain250Hz = 0.0;
    [ObservableProperty] private double _gain500Hz = 0.0;
    [ObservableProperty] private double _gain1kHz = 0.0;
    [ObservableProperty] private double _gain2kHz = 0.0;
    [ObservableProperty] private double _gain4kHz = 0.0;
    [ObservableProperty] private double _gain8kHz = 0.0;
    [ObservableProperty] private double _gain16kHz = 0.0;

    // Acoustic Enhancement Shelves (-6.0 to +12.0 dB)
    [ObservableProperty] private double _bassGain = 0.0;
    [ObservableProperty] private double _trebleGain = 0.0;

    // Preamp & Auto-Preamp Headroom
    [ObservableProperty] private double _preampGain = 0.0;
    [ObservableProperty] private bool _isLoudnessNormalizationEnabled = true;
    [ObservableProperty] private string _autoPreampHeadroomText = "0.0 dB";

    // Stereo Widening Soundstage (0% to 200%, default 100%)
    [ObservableProperty] private bool _isStereoWideningEnabled = false;
    [ObservableProperty] private int _stereoWidth = 100;

    // Dynamics / Dialogue Compressor
    [ObservableProperty] private bool _isCompressorEnabled = false;
    [ObservableProperty] private double _compressorThreshold = -12.0;

    // Master DSP Bypass (A/B Instant Comparison)
    [ObservableProperty] private bool _isBypassed = false;
    [ObservableProperty] private string _bypassButtonText = "Bypass: OFF (Active)";

    // Equalizer APO Subsystem Status
    [ObservableProperty] private bool _isApoInstalled = false;
    [ObservableProperty] private string _apoStatusMessage = "Detecting Equalizer APO...";
    [ObservableProperty] private string _apoGuidanceTip = "";
    [ObservableProperty] private bool _isSystemWideActive = false;
    [ObservableProperty] private string _lastExportMessage = "";
    [ObservableProperty] private string _dspStatusText = "DSP Inactive";
    [ObservableProperty] private System.Windows.Media.Brush _dspStatusBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(107, 114, 128));

    // Spatial Audio & Dolby Atmos Compatibility
    [ObservableProperty] private string _spatialAudioStatusText = "Windows Spatial Sound: Inactive (Direct 2-Channel Bit-Exact)";
    [ObservableProperty] private bool _isSpatialAudioActive = false;
    [ObservableProperty] private bool _isDolbyAccessInstalled = false;
    [ObservableProperty] private bool _isDtsInstalled = false;
    [ObservableProperty] private string _spatialFormatName = "Direct Stereo (100% Bit-Exact)";
    [ObservableProperty] private string _spatialGuidance = "Click to configure Windows Sonic or Dolby Atmos in Windows Sound Settings.";

    // Presets Management & Categories
    public ObservableCollection<AudioProfile> Presets { get; } = new();
    public ObservableCollection<AudioProfile> FilteredPresets { get; } = new();
    public ObservableCollection<AudioPresetItemViewModel> PresetCardItems { get; } = new();
    public ObservableCollection<AudioPresetItemViewModel> FilteredPresetCardItems { get; } = new();
    public ObservableCollection<string> Categories { get; } = new()
    {
        "All",
        "Audiophile / Music",
        "Competitive Gaming",
        "Cinema & Movies",
        "Voice & Podcast",
        "Custom"
    };
    [ObservableProperty] private string _selectedCategory = "All";
    [ObservableProperty] private AudioProfile? _selectedPreset;
    [ObservableProperty] private bool _isCreatingNewPreset = false;
    [ObservableProperty] private string _newPresetName = "";

    // Headphone AutoEq Calibration Profiles
    public ObservableCollection<HeadphoneProfile> HeadphoneProfiles { get; } = new();
    [ObservableProperty] private HeadphoneProfile? _selectedHeadphoneProfile;
    [ObservableProperty] private string _headphoneCalibrationStatus = "Acoustic calibration curve inactive. Select a profile or import AutoEq.";
    [ObservableProperty] private bool _isHeadphoneCalibrationActive = false;

    // Parametric Filter Drawer
    public ObservableCollection<ParametricFilter> ParametricFilters { get; } = new();

    // Live 32-Band Visualizer
    public ObservableCollection<double> SpectrumBars { get; } = new();
    [ObservableProperty] private bool _isVisualizerActive = false;

    // Mathematical Frequency Response Curve
    [ObservableProperty] private PointCollection _curvePoints = new();
    [ObservableProperty] private Geometry? _curveAreaGeometry;

    // View display mode: "Curve", "Spectrum", or "Combined"
    [ObservableProperty] private string _visualizerMode = "Combined";

    public AudioStudioViewModel(
        IAudioService audioService,
        IEqualizerApoBridge apoBridge,
        IProfileService profileService,
        ISpatialAudioService? spatialAudioService = null)
    {
        _audioService = audioService;
        _apoBridge = apoBridge;
        _profileService = profileService;
        _spatialAudioService = spatialAudioService;

        // Initialize 32 spectrum bar amplitudes
        for (int i = 0; i < 32; i++) SpectrumBars.Add(0.0);

        _visualizer = new WasapiLoopbackVisualizer
        {
            SimulateWhenIdle = false
        };
        _visualizer.SpectrumUpdated += OnSpectrumUpdated;
        _audioEngine.AudioPcmRendered += pcm => _visualizer.PushPcmSamples(pcm);

        LoadPresets();
        LoadHeadphoneProfiles();
        RefreshApoStatus();
        RefreshSpatialAudioStatus();
        SyncFromAudioService();
        _ = SyncChannelBalanceAsync();
        UpdateResponseCurve();

        _audioService.AudioEndpointChanged += (s, dev) =>
        {
            ActiveDeviceName = dev.Name;
            MasterVolume = dev.VolumePercent;
            IsMuted = dev.IsMuted;
            _ = SyncChannelBalanceAsync();
            RefreshSpatialAudioStatus();
            _visualizer.Start(dev.Id);
        };

        // Start WASAPI loopback capture
        _visualizer.Start(_audioService.ActiveDevice?.Id);
    }

    private void SyncFromAudioService()
    {
        var dev = _audioService.ActiveDevice;
        if (dev != null)
        {
            ActiveDeviceName = dev.Name;
            MasterVolume = dev.VolumePercent;
            IsMuted = dev.IsMuted;
        }
    }

    private async Task SyncChannelBalanceAsync()
    {
        try
        {
            var res = await _audioService.GetChannelBalanceAsync();
            if (res.IsSuccess)
            {
                ChannelBalanceLeft = res.Value.Left;
                ChannelBalanceRight = res.Value.Right;
                UpdateChannelBalanceText();
            }
        }
        catch { }
    }

    private void UpdateChannelBalanceText()
    {
        int leftPct = (int)Math.Round(ChannelBalanceLeft * 100);
        int rightPct = (int)Math.Round(ChannelBalanceRight * 100);
        if (leftPct == rightPct)
        {
            ChannelBalanceText = $"Center ({leftPct}% / {rightPct}%)";
        }
        else
        {
            ChannelBalanceText = $"L {leftPct}% : R {rightPct}%";
        }
    }

    partial void OnChannelBalanceLeftChanged(float value)
    {
        UpdateChannelBalanceText();
        _ = _audioService.SetChannelBalanceAsync(value, ChannelBalanceRight);
    }

    partial void OnChannelBalanceRightChanged(float value)
    {
        UpdateChannelBalanceText();
        _ = _audioService.SetChannelBalanceAsync(ChannelBalanceLeft, value);
    }

    [RelayCommand]
    public async Task SetBalanceCenter()
    {
        ChannelBalanceLeft = 1.0f;
        ChannelBalanceRight = 1.0f;
        ChannelBalanceText = "Center (100% / 100%)";
        await _audioService.SetChannelBalanceAsync(1.0f, 1.0f);
        OnFilterChanged();
    }

    public void RefreshApoStatus()
    {
        var status = _apoBridge.CheckStatus();
        IsApoInstalled = status.IsInstalled;
        ApoStatusMessage = status.StatusMessage;
        ApoGuidanceTip = status.GuidanceTip;
        IsSystemWideActive = status.IsInstalled && status.IsConfiguredForActiveDevice;
        UpdateDspStatus();
    }

    public void RefreshSpatialAudioStatus()
    {
        if (_spatialAudioService == null) return;
        try
        {
            var devId = _audioService.ActiveDevice?.Id;
            var info = _spatialAudioService.GetSpatialAudioInfo(devId);
            IsSpatialAudioActive = info.IsActive;
            IsDolbyAccessInstalled = info.IsDolbyAccessInstalled;
            IsDtsInstalled = info.IsDtsInstalled;
            SpatialFormatName = info.ActiveFormatName;
            SpatialAudioStatusText = info.StatusMessage;
        }
        catch { }
    }

    private void UpdateDspStatus()
    {
        if (!IsApoInstalled)
        {
            DspStatusText = "DSP Inactive (APO Not Detected)";
            DspStatusBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68)); // #EF4444
        }
        else if (IsBypassed)
        {
            DspStatusText = "DSP Bypassed (Passthrough Mode)";
            DspStatusBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11)); // #F59E0B
        }
        else if (IsSystemWideActive)
        {
            DspStatusText = "DSP Active (Equalizer APO Realtime)";
            DspStatusBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 229, 255)); // #00E5FF
        }
        else
        {
            DspStatusText = "APO Installed (Device Config Pending)";
            DspStatusBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11)); // #F59E0B
        }
    }

    partial void OnSelectedCategoryChanged(string value)
    {
        UpdateFilteredPresets();
    }

    [RelayCommand]
    public void SetCategory(string category)
    {
        if (!string.IsNullOrWhiteSpace(category))
        {
            SelectedCategory = category;
        }
    }

    private readonly Stopwatch _spectrumThrottle = Stopwatch.StartNew();

    private void UpdateFilteredPresets()
    {
        FilteredPresets.Clear();
        FilteredPresetCardItems.Clear();

        // Ensure PresetCardItems matches Presets
        if (PresetCardItems.Count != Presets.Count)
        {
            PresetCardItems.Clear();
            foreach (var p in Presets)
            {
                bool isActive = SelectedPreset != null && p.Id == SelectedPreset.Id;
                PresetCardItems.Add(new AudioPresetItemViewModel(p, isActive));
            }
        }
        else
        {
            foreach (var card in PresetCardItems)
            {
                card.IsActive = (SelectedPreset != null && card.Id == SelectedPreset.Id);
            }
        }

        foreach (var p in Presets)
        {
            if (SelectedCategory == "All")
            {
                FilteredPresets.Add(p);
            }
            else if (SelectedCategory == "Custom")
            {
                if (p.IsCustom) FilteredPresets.Add(p);
            }
            else if (string.Equals(p.Category, SelectedCategory, StringComparison.OrdinalIgnoreCase))
            {
                FilteredPresets.Add(p);
            }
        }

        foreach (var card in PresetCardItems)
        {
            if (SelectedCategory == "All")
            {
                FilteredPresetCardItems.Add(card);
            }
            else if (SelectedCategory == "Custom")
            {
                if (card.IsCustom) FilteredPresetCardItems.Add(card);
            }
            else if (string.Equals(card.Category, SelectedCategory, StringComparison.OrdinalIgnoreCase))
            {
                FilteredPresetCardItems.Add(card);
            }
        }
    }

    private void LoadPresets()
    {
        Presets.Clear();
        PresetCardItems.Clear();
        foreach (var p in _profileService.GetAudioProfiles())
        {
            Presets.Add(p);
            PresetCardItems.Add(new AudioPresetItemViewModel(p, false));
        }

        SelectedPreset = Presets.FirstOrDefault();
        UpdateFilteredPresets();
    }

    private void LoadHeadphoneProfiles()
    {
        HeadphoneProfiles.Clear();
        foreach (var hp in _profileService.GetHeadphoneProfiles())
        {
            HeadphoneProfiles.Add(hp);
        }
    }

    private bool _wasAllSilent = true;

    private void OnSpectrumUpdated(float[] bands)
    {
        if (!IsVisualizerActive) return;

        // 33ms frame pacing (~30 FPS throttle) to keep Dispatcher queue completely unblocked
        if (_spectrumThrottle.ElapsedMilliseconds < 33) return;
        _spectrumThrottle.Restart();

        // Check if all bands are effectively silent
        bool isAllSilent = true;
        for (int i = 0; i < bands.Length; i++)
        {
            if (bands[i] > 0.005f)
            {
                isAllSilent = false;
                break;
            }
        }

        if (isAllSilent && _wasAllSilent) return;
        _wasAllSilent = isAllSilent;

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null) return;

        dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () =>
        {
            if (!IsVisualizerActive) return;
            for (int i = 0; i < bands.Length && i < SpectrumBars.Count; i++)
            {
                double targetVal = bands[i] * 120.0;
                if (Math.Abs(SpectrumBars[i] - targetVal) > 0.75)
                {
                    SpectrumBars[i] = targetVal;
                }
            }
        });
    }

    public void UpdateResponseCurve()
    {
        var currentProfile = BuildCurrentProfile();
        
        // Auto-Preamp Headroom calculation
        double maxPos = 0.0;
        foreach (var g in currentProfile.BandGains) if (g > maxPos) maxPos = g;
        if (currentProfile.BassGainDb > maxPos) maxPos = currentProfile.BassGainDb;
        if (currentProfile.TrebleGainDb > maxPos) maxPos = currentProfile.TrebleGainDb;

        if (IsLoudnessNormalizationEnabled && maxPos > 0.0)
        {
            AutoPreampHeadroomText = $"-{maxPos:F1} dB Headroom";
        }
        else
        {
            AutoPreampHeadroomText = "0.0 dB (Direct)";
        }

        // Generate smooth curve points (width 780, height 130)
        var points = AudioResponseCalculator.GenerateResponseCurvePoints(currentProfile, 780, 130, 160);
        CurvePoints = points;
        CurveAreaGeometry = AudioResponseCalculator.GenerateAreaGeometry(points, 780, 130);
    }

    private AudioProfile BuildCurrentProfile()
    {
        var profile = new AudioProfile
        {
            Name = SelectedPreset?.Name ?? "Custom Profile",
            BandGains = new double[]
            {
                Gain32Hz, Gain64Hz, Gain125Hz, Gain250Hz, Gain500Hz,
                Gain1kHz, Gain2kHz, Gain4kHz, Gain8kHz, Gain16kHz
            },
            BassGainDb = BassGain,
            TrebleGainDb = TrebleGain,
            PreampDb = PreampGain,
            IsLoudnessNormalizationEnabled = IsLoudnessNormalizationEnabled,
            IsStereoWideningEnabled = IsStereoWideningEnabled,
            StereoWidthPercent = StereoWidth,
            IsCompressorEnabled = IsCompressorEnabled,
            CompressorThresholdDb = CompressorThreshold,
            IsBypassed = IsBypassed,
            TargetDeviceName = ActiveDeviceName
        };

        foreach (var pf in ParametricFilters)
        {
            profile.ParametricFilters.Add(pf);
        }

        return profile;
    }

    private void OnFilterChanged()
    {
        UpdateResponseCurve();
        var currentProfile = BuildCurrentProfile();
        _audioEngine.UpdateDspParameters(currentProfile, ChannelBalanceLeft, ChannelBalanceRight);
        _audioEngine.SetBypass(IsBypassed);

        _apoDebouncer.Debounce(() =>
        {
            if (IsApoInstalled)
            {
                var profile = BuildCurrentProfile();
                _ = _apoBridge.ApplyProfileAsync(profile);
            }
        });
    }

    partial void OnGain32HzChanged(double value) => OnFilterChanged();
    partial void OnGain64HzChanged(double value) => OnFilterChanged();
    partial void OnGain125HzChanged(double value) => OnFilterChanged();
    partial void OnGain250HzChanged(double value) => OnFilterChanged();
    partial void OnGain500HzChanged(double value) => OnFilterChanged();
    partial void OnGain1kHzChanged(double value) => OnFilterChanged();
    partial void OnGain2kHzChanged(double value) => OnFilterChanged();
    partial void OnGain4kHzChanged(double value) => OnFilterChanged();
    partial void OnGain8kHzChanged(double value) => OnFilterChanged();
    partial void OnGain16kHzChanged(double value) => OnFilterChanged();
    partial void OnBassGainChanged(double value) => OnFilterChanged();
    partial void OnTrebleGainChanged(double value) => OnFilterChanged();
    partial void OnPreampGainChanged(double value) => OnFilterChanged();
    partial void OnIsLoudnessNormalizationEnabledChanged(bool value) => OnFilterChanged();
    partial void OnIsStereoWideningEnabledChanged(bool value) => OnFilterChanged();
    partial void OnStereoWidthChanged(int value) => OnFilterChanged();
    partial void OnIsCompressorEnabledChanged(bool value) => OnFilterChanged();
    partial void OnCompressorThresholdChanged(double value) => OnFilterChanged();

    partial void OnMasterVolumeChanged(int value)
    {
        _ = _audioService.SetVolumeAsync(value);
    }

    [RelayCommand]
    public async Task ToggleMute()
    {
        IsMuted = !IsMuted;
        await _audioService.SetMuteAsync(IsMuted);
    }

    [RelayCommand]
    public async Task SwitchOutputDevice()
    {
        await _audioService.ToggleOutputDeviceAsync();
    }

    [RelayCommand]
    public void ToggleBypass()
    {
        IsBypassed = !IsBypassed;
        BypassButtonText = IsBypassed ? "Bypass: ON (Passthrough)" : "Bypass: OFF (Active)";
        UpdateDspStatus();
        OnFilterChanged();
    }

    [RelayCommand]
    public void ApplyPresetCard(AudioPresetItemViewModel card)
    {
        if (card?.Profile != null)
        {
            ApplyPreset(card.Profile);
        }
    }

    [RelayCommand]
    public void ApplyPreset(AudioProfile preset)
    {
        if (preset == null) return;
        SelectedPreset = preset;

        foreach (var card in PresetCardItems)
        {
            card.IsActive = (card.Id == preset.Id);
        }

        if (preset.BandGains?.Length >= 10)
        {
            Gain32Hz = preset.BandGains[0];
            Gain64Hz = preset.BandGains[1];
            Gain125Hz = preset.BandGains[2];
            Gain250Hz = preset.BandGains[3];
            Gain500Hz = preset.BandGains[4];
            Gain1kHz = preset.BandGains[5];
            Gain2kHz = preset.BandGains[6];
            Gain4kHz = preset.BandGains[7];
            Gain8kHz = preset.BandGains[8];
            Gain16kHz = preset.BandGains[9];
        }

        BassGain = preset.BassGainDb;
        TrebleGain = preset.TrebleGainDb;
        PreampGain = preset.PreampDb;
        IsLoudnessNormalizationEnabled = preset.IsLoudnessNormalizationEnabled;
        IsCompressorEnabled = preset.IsCompressorEnabled;
        CompressorThreshold = preset.CompressorThresholdDb;
        IsStereoWideningEnabled = preset.IsStereoWideningEnabled;
        StereoWidth = preset.StereoWidthPercent;
        IsBypassed = preset.IsBypassed;
        BypassButtonText = IsBypassed ? "Bypass: ON (Passthrough)" : "Bypass: OFF (Active)";

        ParametricFilters.Clear();
        if (preset.ParametricFilters != null)
        {
            foreach (var pf in preset.ParametricFilters)
            {
                ParametricFilters.Add(pf);
            }
        }

        OnFilterChanged();
    }

    [RelayCommand]
    public void ApplyHeadphoneProfile(HeadphoneProfile profile)
    {
        if (profile == null) return;
        SelectedHeadphoneProfile = profile;
        IsHeadphoneCalibrationActive = true;
        HeadphoneCalibrationStatus = $"Calibrated: {profile.Name} ({profile.TargetCurve})";

        PreampGain = profile.PreampDb;

        ParametricFilters.Clear();
        foreach (var f in profile.ParametricFilters)
        {
            ParametricFilters.Add(new ParametricFilter
            {
                IsEnabled = f.IsEnabled,
                Type = f.Type,
                Frequency = f.Frequency,
                GainDb = f.GainDb,
                Q = f.Q
            });
        }

        OnFilterChanged();
    }

    [RelayCommand]
    public void ClearHeadphoneCalibration()
    {
        SelectedHeadphoneProfile = null;
        IsHeadphoneCalibrationActive = false;
        HeadphoneCalibrationStatus = "Calibration curve inactive.";
        ParametricFilters.Clear();
        PreampGain = 0.0;
        OnFilterChanged();
    }

    [RelayCommand]
    public async Task ImportAutoEqFile(string? filePath = null)
    {
        try
        {
            string targetPath = filePath ?? "";
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Import AutoEq Parametric File",
                    Filter = "AutoEq Parametric File (*.txt;*.csv)|*.txt;*.csv|All Files (*.*)|*.*",
                    Multiselect = false
                };

                if (dlg.ShowDialog() == true)
                {
                    targetPath = dlg.FileName;
                }
            }

            if (!string.IsNullOrWhiteSpace(targetPath))
            {
                var res = await _profileService.ImportAutoEqProfileAsync(targetPath);
                if (res.IsSuccess && res.Value != null)
                {
                    HeadphoneProfiles.Add(res.Value);
                    ApplyHeadphoneProfile(res.Value);
                    LastExportMessage = $"Imported AutoEq profile: {res.Value.Name}";
                }
                else
                {
                    LastExportMessage = $"Import failed: {res.Error}";
                }
            }
        }
        catch (Exception ex)
        {
            LastExportMessage = $"Import error: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task DeleteHeadphoneProfile(HeadphoneProfile profile)
    {
        if (profile == null || !profile.IsCustom) return;
        await _profileService.DeleteHeadphoneProfileAsync(profile.Id);
        HeadphoneProfiles.Remove(profile);
        if (SelectedHeadphoneProfile?.Id == profile.Id)
        {
            ClearHeadphoneCalibration();
        }
    }

    [RelayCommand]
    public void OpenWindowsSoundSettings()
    {
        _spatialAudioService?.OpenWindowsSoundSettings();
    }

    [RelayCommand]
    public void OpenDolbyAccess()
    {
        _spatialAudioService?.OpenDolbyAccess();
    }

    [RelayCommand]
    public void ResetEq()
    {
        Gain32Hz = 0;
        Gain64Hz = 0;
        Gain125Hz = 0;
        Gain250Hz = 0;
        Gain500Hz = 0;
        Gain1kHz = 0;
        Gain2kHz = 0;
        Gain4kHz = 0;
        Gain8kHz = 0;
        Gain16kHz = 0;
        BassGain = 0;
        TrebleGain = 0;
        PreampGain = 0;
        IsCompressorEnabled = false;
        IsStereoWideningEnabled = false;
        StereoWidth = 100;
        IsBypassed = false;
        BypassButtonText = "Bypass: OFF (Active)";

        OnFilterChanged();
    }

    [RelayCommand]
    public void InvertEq()
    {
        Gain32Hz = -Gain32Hz;
        Gain64Hz = -Gain64Hz;
        Gain125Hz = -Gain125Hz;
        Gain250Hz = -Gain250Hz;
        Gain500Hz = -Gain500Hz;
        Gain1kHz = -Gain1kHz;
        Gain2kHz = -Gain2kHz;
        Gain4kHz = -Gain4kHz;
        Gain8kHz = -Gain8kHz;
        Gain16kHz = -Gain16kHz;

        OnFilterChanged();
    }

    [RelayCommand]
    public async Task CreateNewPreset()
    {
        string name = string.IsNullOrWhiteSpace(NewPresetName) ? $"Custom Tuning {Presets.Count + 1}" : NewPresetName.Trim();
        var currentGains = new double[]
        {
            Gain32Hz, Gain64Hz, Gain125Hz, Gain250Hz, Gain500Hz,
            Gain1kHz, Gain2kHz, Gain4kHz, Gain8kHz, Gain16kHz
        };

        var result = await _profileService.CreateCustomAudioProfileAsync(name, currentGains, BassGain, TrebleGain);
        if (result.IsSuccess && result.Value != null)
        {
            Presets.Add(result.Value);
            UpdateFilteredPresets();
            SelectedPreset = result.Value;
            IsCreatingNewPreset = false;
            NewPresetName = "";
        }
    }

    [RelayCommand]
    public async Task DuplicateCurrentPreset()
    {
        if (SelectedPreset == null) return;
        var result = await _profileService.DuplicateAudioProfileAsync(SelectedPreset.Id);
        if (result.IsSuccess && result.Value != null)
        {
            Presets.Add(result.Value);
            UpdateFilteredPresets();
            SelectedPreset = result.Value;
        }
    }

    [RelayCommand]
    public async Task DeleteCurrentPreset()
    {
        if (SelectedPreset == null || !SelectedPreset.IsCustom) return;

        var toDelete = SelectedPreset;
        await _profileService.DeleteAudioProfileAsync(toDelete.Id);
        Presets.Remove(toDelete);
        UpdateFilteredPresets();
        SelectedPreset = Presets.FirstOrDefault();
        if (SelectedPreset != null) ApplyPreset(SelectedPreset);
    }

    [RelayCommand]
    public void AddParametricFilter()
    {
        var newFilter = new ParametricFilter
        {
            Frequency = 1000.0,
            GainDb = 3.0,
            Q = 1.414,
            Type = ParametricFilterType.Peaking,
            IsEnabled = true
        };
        ParametricFilters.Add(newFilter);
        OnFilterChanged();
    }

    [RelayCommand]
    public void RemoveParametricFilter(ParametricFilter filter)
    {
        if (filter != null)
        {
            ParametricFilters.Remove(filter);
            OnFilterChanged();
        }
    }

    [RelayCommand]
    public void OpenApoDownloadWebsite()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "https://sourceforge.net/projects/equalizerapo/",
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to open browser: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task ExportDspConfig()
    {
        try
        {
            string exportDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PRISM");
            string exportPath = Path.Combine(exportDir, "prism_dsp_export.txt");

            var profile = BuildCurrentProfile();
            var result = await _apoBridge.ExportConfigAsync(profile, exportPath);
            if (result.IsSuccess)
            {
                LastExportMessage = $"DSP configuration exported to: {exportPath}";
            }
            else
            {
                LastExportMessage = $"Export failed: {result.Error}";
            }
        }
        catch (Exception ex)
        {
            LastExportMessage = $"Export error: {ex.Message}";
        }
    }

    [RelayCommand]
    public void SetVisualizerMode(string mode)
    {
        if (!string.IsNullOrWhiteSpace(mode))
        {
            VisualizerMode = mode;
        }
    }

    [RelayCommand]
    public async Task ExportPreset()
    {
        if (SelectedPreset == null) return;

        try
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export PRISM EQ Preset",
                Filter = "PRISM EQ Preset (*.prism-eq)|*.prism-eq|JSON File (*.json)|*.json",
                FileName = $"{SelectedPreset.Name.Replace(" ", "_")}.prism-eq",
                DefaultExt = ".prism-eq"
            };

            if (dlg.ShowDialog() == true)
            {
                var result = await _profileService.ExportAudioPresetAsync(SelectedPreset.Id, dlg.FileName);
                if (result.IsSuccess)
                {
                    LastExportMessage = $"Exported '{SelectedPreset.Name}' to {Path.GetFileName(dlg.FileName)}";
                }
                else
                {
                    LastExportMessage = $"Export failed: {result.Error}";
                }
            }
        }
        catch (Exception ex)
        {
            LastExportMessage = $"Export error: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task ImportPreset()
    {
        try
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Import PRISM EQ Preset",
                Filter = "PRISM EQ Preset (*.prism-eq)|*.prism-eq|JSON File (*.json)|*.json",
                Multiselect = false
            };

            if (dlg.ShowDialog() == true)
            {
                var result = await _profileService.ImportAudioPresetAsync(dlg.FileName);
                if (result.IsSuccess && result.Value != null)
                {
                    Presets.Add(result.Value);
                    UpdateFilteredPresets();
                    SelectedPreset = result.Value;
                    ApplyPreset(result.Value);
                    LastExportMessage = $"Imported '{result.Value.Name}' preset successfully.";
                }
                else
                {
                    LastExportMessage = $"Import failed: {result.Error}";
                }
            }
        }
        catch (Exception ex)
        {
            LastExportMessage = $"Import error: {ex.Message}";
        }
    }

    [RelayCommand]
    public void LaunchConfigurator()
    {
        try
        {
            string[] possiblePaths = new[]
            {
                @"C:\Program Files\EqualizerAPO\Configurator.exe",
                @"C:\Program Files (x86)\EqualizerAPO\Configurator.exe"
            };

            string? targetPath = possiblePaths.FirstOrDefault(File.Exists);

            if (targetPath != null)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = targetPath,
                    UseShellExecute = true
                });
                ApoStatusMessage = "Equalizer APO Configurator launched.";
            }
            else
            {
                OpenApoDownloadWebsite();
            }
        }
        catch (Exception ex)
        {
            ApoStatusMessage = $"Configurator launch error: {ex.Message}";
        }
    }

    [RelayCommand]
    public void ToggleAudition()
    {
        if (IsAuditionPlaying)
        {
            _audioEngine.StopAudition();
            IsAuditionPlaying = false;
            AuditionButtonText = "▶ Play Audition Track";
        }
        else
        {
            var p = BuildCurrentProfile();
            _audioEngine.UpdateDspParameters(p, ChannelBalanceLeft, ChannelBalanceRight);
            _audioEngine.SetBypass(IsBypassed);
            _audioEngine.StartAudition(SelectedAuditionTrack);
            IsAuditionPlaying = true;
            AuditionButtonText = "■ Stop Audition";
        }
    }

    [RelayCommand]
    public void SelectAuditionTrack(string trackName)
    {
        switch (trackName?.ToLowerInvariant())
        {
            case "acoustic":
                SelectedAuditionTrack = AuditionTrack.AcousticMelody;
                AuditionTrackName = "Acoustic Melody (Harmonics & Treble)";
                break;
            case "speech":
            case "voice":
                SelectedAuditionTrack = AuditionTrack.SpeechVoice;
                AuditionTrackName = "Vocal Speech (Midrange Formants)";
                break;
            case "noise":
            case "pink":
                SelectedAuditionTrack = AuditionTrack.PinkNoise;
                AuditionTrackName = "Pink Noise (20 Hz - 20 kHz Flat Spectrum)";
                break;
            default:
                SelectedAuditionTrack = AuditionTrack.SynthGroove;
                AuditionTrackName = "Dynamic Synth Groove (Kick, Bass & Synth)";
                break;
        }

        if (IsAuditionPlaying)
        {
            _audioEngine.CurrentTrack = SelectedAuditionTrack;
        }
    }

    [RelayCommand]
    public void ResetEnhancements()
    {
        BassGain = 0;
        TrebleGain = 0;
        PreampGain = 0;
        IsCompressorEnabled = false;
        CompressorThreshold = -12.0;
        IsStereoWideningEnabled = false;
        StereoWidth = 100;
        OnFilterChanged();
    }

    [RelayCommand]
    public void ResetAllAudioSettings()
    {
        var flat = Presets.FirstOrDefault(p => p.Id == "aud_flat");
        if (flat != null)
        {
            ApplyPreset(flat);
        }
        else
        {
            ResetEq();
        }
        ResetEnhancements();
        _ = SetBalanceCenter();
    }

    public void Dispose()
    {
        _audioEngine.Dispose();
        _visualizer.Dispose();
        _apoDebouncer.Dispose();
    }
}

public partial class AudioPresetItemViewModel : ObservableObject
{
    public AudioProfile Profile { get; }
    public string Id => Profile.Id;
    public string Name => Profile.Name;
    public string Category => Profile.Category;
    public string Description => Profile.Description;
    public string IntendedExperience => Profile.IntendedExperience;
    public bool IsCustom => Profile.IsCustom;
    public string MiniCurvePath { get; }

    [ObservableProperty]
    private bool _isActive;

    public AudioPresetItemViewModel(AudioProfile profile, bool isActive = false)
    {
        Profile = profile;
        IsActive = isActive;
        MiniCurvePath = AudioResponseCalculator.GenerateMiniSvgPath(profile, 90.0, 24.0);
    }
}
