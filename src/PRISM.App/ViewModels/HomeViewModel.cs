using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PRISM.Core.Interfaces;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PRISM.App.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private readonly IDisplayService _displayService;
    private readonly IAudioService _audioService;
    private readonly IEnvironmentService _environmentService;

    [ObservableProperty]
    private string _monitorName = "Acer ED270 X0";

    [ObservableProperty]
    private string _monitorStatus = "DDC/CI Active • DisplayPort";

    [ObservableProperty]
    private string _monitorConnectionStatus = "Connected";

    [ObservableProperty]
    private int _brightness = 45;

    [ObservableProperty]
    private string _monitorResolutionRate = "1920 × 1080 (FHD) · 120 Hz";

    [ObservableProperty]
    private string _audioDeviceSpecs = "Stereo · 48 kHz";

    [ObservableProperty]
    private int _contrast = 58;

    [ObservableProperty]
    private int _redGain = 53;

    [ObservableProperty]
    private int _greenGain = 59;

    [ObservableProperty]
    private int _blueGain = 31;

    [ObservableProperty]
    private string _selectedAudioCategory = "Movies";

    [ObservableProperty]
    private string _activeAudioPresetName = "Cinematic Movie";

    [ObservableProperty]
    private string _activeAudioPresetDescription = "A cinematic profile with enhanced bass, clear dialogues and a wide soundstage.";

    [ObservableProperty]
    private string _activeAudioPresetCurvePath = "M 0 16 C 15 8, 30 20, 45 12 C 60 4, 75 16, 90 10";

    [ObservableProperty]
    private bool _isAudioBypassed = false;

    private DisplayStudioViewModel? _displayVm;
    private AudioStudioViewModel? _audioVm;
    private bool _isSyncing = false;

    public void AttachStudios(DisplayStudioViewModel displayVm, AudioStudioViewModel audioVm)
    {
        _displayVm = displayVm;
        _audioVm = audioVm;

        try
        {
            _isSyncing = true;
            // Initialize from studios
            Brightness = _displayVm.Brightness;
            Contrast = _displayVm.Contrast;
            RedGain = _displayVm.RedGain;
            GreenGain = _displayVm.GreenGain;
            BlueGain = _displayVm.BlueGain;
            Volume = _audioVm.MasterVolume;
            IsMuted = _audioVm.IsMuted;
        }
        finally
        {
            _isSyncing = false;
        }

        _displayVm.PropertyChanged += (s, e) =>
        {
            if (_isSyncing) return;
            try
            {
                _isSyncing = true;
                if (e.PropertyName == nameof(DisplayStudioViewModel.Brightness)) Brightness = _displayVm.Brightness;
                if (e.PropertyName == nameof(DisplayStudioViewModel.Contrast)) Contrast = _displayVm.Contrast;
                if (e.PropertyName == nameof(DisplayStudioViewModel.RedGain)) RedGain = _displayVm.RedGain;
                if (e.PropertyName == nameof(DisplayStudioViewModel.GreenGain)) GreenGain = _displayVm.GreenGain;
                if (e.PropertyName == nameof(DisplayStudioViewModel.BlueGain)) BlueGain = _displayVm.BlueGain;
            }
            finally
            {
                _isSyncing = false;
            }
        };

        _audioVm.PropertyChanged += (s, e) =>
        {
            if (_isSyncing) return;
            try
            {
                _isSyncing = true;
                if (e.PropertyName == nameof(AudioStudioViewModel.MasterVolume)) Volume = _audioVm.MasterVolume;
                if (e.PropertyName == nameof(AudioStudioViewModel.IsMuted)) IsMuted = _audioVm.IsMuted;
                if (e.PropertyName == nameof(AudioStudioViewModel.SelectedPreset) && _audioVm.SelectedPreset != null)
                {
                    ActiveAudioPresetName = _audioVm.SelectedPreset.Name;
                    ActiveAudioPresetDescription = _audioVm.SelectedPreset.Description;
                }
            }
            finally
            {
                _isSyncing = false;
            }
        };
    }

    [ObservableProperty]
    private string _audioDeviceName = "Cosmic Byte Proteus";

    [ObservableProperty]
    private string _audioDeviceType = "Headphones • 48 kHz";

    [ObservableProperty]
    private string _audioEndpointStatus = "Active";

    [ObservableProperty]
    private string _dspEngineName = "Equalizer APO";

    [ObservableProperty]
    private string _dspEngineStatus = "Not Installed";

    [ObservableProperty]
    private int _volume = 75;

    [ObservableProperty]
    private bool _isMuted = false;

    [ObservableProperty]
    private bool _isSubZeroDimEnabled = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDaylightFocusActive))]
    [NotifyPropertyChangedFor(nameof(IsLateNightActive))]
    [NotifyPropertyChangedFor(nameof(IsCompetitiveGamingActive))]
    [NotifyPropertyChangedFor(nameof(IsCinematicMovieActive))]
    private string _activeEnvironment = "Late Night";

    public bool IsDaylightFocusActive => ActiveEnvironment.Equals("Daylight Focus", StringComparison.OrdinalIgnoreCase);
    public bool IsLateNightActive => ActiveEnvironment.Equals("Late Night", StringComparison.OrdinalIgnoreCase);
    public bool IsCompetitiveGamingActive => ActiveEnvironment.Equals("Competitive Gaming", StringComparison.OrdinalIgnoreCase);
    public bool IsCinematicMovieActive => ActiveEnvironment.Equals("Cinematic Movie", StringComparison.OrdinalIgnoreCase) || ActiveEnvironment.Equals("Cinematic", StringComparison.OrdinalIgnoreCase);

    public event Action<string>? NavigationRequested;
    public event Action<string>? SelectEnvironmentRequested;

    public HomeViewModel(
        IDisplayService displayService,
        IAudioService audioService,
        IEnvironmentService environmentService)
    {
        _displayService = displayService;
        _audioService = audioService;
        _environmentService = environmentService;

        RefreshState();

        _displayService.DisplayConfigChanged += (s, m) =>
        {
            try
            {
                _isSyncing = true;
                MonitorName = m.Name;
                Brightness = m.CurrentBrightness;
                Contrast = m.CurrentContrast;
                RedGain = m.RedGain;
                GreenGain = m.GreenGain;
                BlueGain = m.BlueGain;
                MonitorStatus = $"{m.Capabilities.DiagnosticNotes} • {m.ConnectionType}";
                MonitorConnectionStatus = m != null ? "Connected" : "Disconnected";
            }
            finally
            {
                _isSyncing = false;
            }
        };

        _audioService.AudioEndpointChanged += (s, a) =>
        {
            try
            {
                _isSyncing = true;
                AudioDeviceName = a.Name;
                Volume = a.VolumePercent;
                IsMuted = a.IsMuted;
                AudioDeviceType = $"{a.DeviceType} • Active Endpoint";
                AudioEndpointStatus = "Active";
            }
            finally
            {
                _isSyncing = false;
            }
        };
    }

    public void RefreshState()
    {
        try
        {
            _isSyncing = true;
            var mon = _displayService.ActiveMonitor;
            if (mon != null)
            {
                MonitorName = mon.Name;
                Brightness = mon.CurrentBrightness;
                Contrast = mon.CurrentContrast;
                RedGain = mon.RedGain;
                GreenGain = mon.GreenGain;
                BlueGain = mon.BlueGain;
                MonitorStatus = $"DDC/CI Hardware Active • {mon.ConnectionType}";
                MonitorConnectionStatus = "Connected";
            }

            var dev = _audioService.ActiveDevice;
            if (dev != null)
            {
                AudioDeviceName = dev.Name;
                Volume = dev.VolumePercent;
                IsMuted = dev.IsMuted;
                AudioDeviceType = $"{dev.DeviceType} • Active Endpoint";
                AudioEndpointStatus = "Active";
            }

            var env = _environmentService.ActivePreset;
            if (env != null)
            {
                ActiveEnvironment = env.Name;
            }
        }
        finally
        {
            _isSyncing = false;
        }
    }

    public void UpdateActiveEnvironment(string name)
    {
        ActiveEnvironment = name;
    }

    partial void OnBrightnessChanged(int value)
    {
        if (_isSyncing) return;
        if (_displayVm != null && _displayVm.Brightness != value)
        {
            _displayVm.Brightness = value;
        }
        else if (_displayService.ActiveMonitor != null)
        {
            _ = _displayService.SetBrightnessAsync(_displayService.ActiveMonitor.Id, value);
        }
    }

    partial void OnContrastChanged(int value)
    {
        if (_isSyncing) return;
        if (_displayVm != null && _displayVm.Contrast != value)
        {
            _displayVm.Contrast = value;
        }
        else if (_displayService.ActiveMonitor != null)
        {
            _ = _displayService.SetContrastAsync(_displayService.ActiveMonitor.Id, value);
        }
    }

    partial void OnRedGainChanged(int value)
    {
        if (_isSyncing) return;
        if (_displayVm != null && _displayVm.RedGain != value)
        {
            _displayVm.RedGain = value;
        }
        else if (_displayService.ActiveMonitor != null)
        {
            _ = _displayService.SetRgbGainsAsync(_displayService.ActiveMonitor.Id, RedGain, GreenGain, BlueGain);
        }
    }

    partial void OnGreenGainChanged(int value)
    {
        if (_isSyncing) return;
        if (_displayVm != null && _displayVm.GreenGain != value)
        {
            _displayVm.GreenGain = value;
        }
        else if (_displayService.ActiveMonitor != null)
        {
            _ = _displayService.SetRgbGainsAsync(_displayService.ActiveMonitor.Id, RedGain, GreenGain, BlueGain);
        }
    }

    partial void OnBlueGainChanged(int value)
    {
        if (_isSyncing) return;
        if (_displayVm != null && _displayVm.BlueGain != value)
        {
            _displayVm.BlueGain = value;
        }
        else if (_displayService.ActiveMonitor != null)
        {
            _ = _displayService.SetRgbGainsAsync(_displayService.ActiveMonitor.Id, RedGain, GreenGain, BlueGain);
        }
    }

    partial void OnVolumeChanged(int value)
    {
        if (_isSyncing) return;
        if (_audioVm != null && _audioVm.MasterVolume != value)
        {
            _audioVm.MasterVolume = value;
        }
        else
        {
            _ = _audioService.SetVolumeAsync(value);
        }
    }

    partial void OnIsSubZeroDimEnabledChanged(bool value)
    {
        if (_isSyncing) return;
        if (_displayService.ActiveMonitor != null)
        {
            _ = _displayService.SetSubZeroDimAsync(_displayService.ActiveMonitor.Id, value, 20);
        }
    }

    [RelayCommand]
    public void FilterAudioCategory(string category)
    {
        SelectedAudioCategory = category;
        if (_audioVm != null)
        {
            _audioVm.SelectedCategory = category;
        }
    }

    [RelayCommand]
    public void ApplyQuickAudioProfile()
    {
        if (_audioVm != null && _audioVm.SelectedPreset != null)
        {
            _audioVm.ApplyPresetCommand.Execute(_audioVm.SelectedPreset);
        }
    }

    [RelayCommand]
    public void ToggleAudioAB()
    {
        IsAudioBypassed = !IsAudioBypassed;
        if (_audioVm != null)
        {
            _audioVm.IsBypassed = IsAudioBypassed;
        }
    }

    [RelayCommand]
    public void ResetQuickAudio()
    {
        if (_audioVm != null)
        {
            _audioVm.ResetAllAudioSettingsCommand.Execute(null);
        }
    }

    [RelayCommand]
    public void OpenDisplayStudio()
    {
        NavigationRequested?.Invoke("Display");
    }

    [RelayCommand]
    public void OpenAudioStudio()
    {
        NavigationRequested?.Invoke("Audio");
    }

    [RelayCommand]
    public void OpenEnvironments()
    {
        NavigationRequested?.Invoke("Environments");
    }

    [RelayCommand]
    public void SelectEnvironment(string presetName)
    {
        SelectEnvironmentRequested?.Invoke(presetName);
    }

    [RelayCommand]
    public async Task ToggleMute()
    {
        IsMuted = !IsMuted;
        await _audioService.SetMuteAsync(IsMuted);
    }

    [RelayCommand]
    public void ToggleSubZero()
    {
        IsSubZeroDimEnabled = !IsSubZeroDimEnabled;
    }

    [RelayCommand]
    public async Task ToggleAudioDevice()
    {
        await _audioService.ToggleOutputDeviceAsync();
    }
}
