using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PRISM.Core.Interfaces;
using PRISM.Core.Models;

namespace PRISM.App.ViewModels;

public partial class QuickControlFlyoutViewModel : ObservableObject
{
    private readonly IDisplayService _displayService;
    private readonly IAudioService _audioService;
    private readonly IEnvironmentService _environmentService;
    private readonly IEqualizerApoBridge _apoBridge;
    private readonly Action<string> _navigateAction;
    private bool _isSyncing = false;

    [ObservableProperty] private string _activeMonitorName = "Acer ED270 X0";
    [ObservableProperty] private int _brightness = 50;
    [ObservableProperty] private bool _isSubZeroDimEnabled = false;
    [ObservableProperty] private int _subZeroDimPercent = 0;

    [ObservableProperty] private string _activeDeviceName = "Cosmic Byte Proteus";
    [ObservableProperty] private int _masterVolume = 75;
    [ObservableProperty] private bool _isMuted = false;

    [ObservableProperty] private string _activeEnvironmentName = "Daylight Focus";
    [ObservableProperty] private string _feedbackMessage = "Ready";
    [ObservableProperty] private bool _isCompactMode = false;

    public ObservableCollection<EnvironmentPreset> Presets { get; } = new();

    public event Action? RequestClose;
    public event Action? CompactModeChanged;

    public QuickControlFlyoutViewModel(
        IDisplayService displayService,
        IAudioService audioService,
        IEnvironmentService environmentService,
        IEqualizerApoBridge apoBridge,
        Action<string> navigateAction)
    {
        _displayService = displayService;
        _audioService = audioService;
        _environmentService = environmentService;
        _apoBridge = apoBridge;
        _navigateAction = navigateAction;

        SyncState();

        _displayService.HardwareStatusUpdated += (s, st) =>
        {
            if (st.LastVerifiedBrightness.HasValue)
            {
                try
                {
                    _isSyncing = true;
                    Brightness = st.LastVerifiedBrightness.Value;
                }
                finally
                {
                    _isSyncing = false;
                }
            }
        };

        _audioService.AudioEndpointChanged += (s, dev) =>
        {
            try
            {
                _isSyncing = true;
                ActiveDeviceName = dev.Name;
                MasterVolume = dev.VolumePercent;
                IsMuted = dev.IsMuted;
            }
            finally
            {
                _isSyncing = false;
            }
        };

        _environmentService.EnvironmentApplied += (s, res) =>
        {
            ActiveEnvironmentName = res.EnvironmentName;
            FeedbackMessage = res.FeedbackMessage;
            foreach (var p in Presets)
            {
                p.IsActive = (p.Name == res.EnvironmentName);
            }
        };
    }

    public void SyncState()
    {
        try
        {
            _isSyncing = true;
            var mon = _displayService.ActiveMonitor;
            if (mon != null)
            {
                ActiveMonitorName = mon.Name;
                Brightness = mon.CurrentBrightness;
                IsSubZeroDimEnabled = mon.IsSubZeroDimEnabled;
                SubZeroDimPercent = mon.SubZeroDimPercent;
            }

            var dev = _audioService.ActiveDevice;
            if (dev != null)
            {
                ActiveDeviceName = dev.Name;
                MasterVolume = dev.VolumePercent;
                IsMuted = dev.IsMuted;
            }

            Presets.Clear();
            foreach (var p in _environmentService.GetPresets())
            {
                Presets.Add(p);
                if (p.IsActive) ActiveEnvironmentName = p.Name;
            }
        }
        finally
        {
            _isSyncing = false;
        }
    }

    partial void OnBrightnessChanged(int value)
    {
        if (_isSyncing) return;
        var mon = _displayService.ActiveMonitor;
        if (mon != null)
        {
            _ = _displayService.SetBrightnessAsync(mon.Id, value);
        }
    }

    partial void OnIsSubZeroDimEnabledChanged(bool value)
    {
        if (_isSyncing) return;
        var mon = _displayService.ActiveMonitor;
        if (mon != null)
        {
            _ = _displayService.SetSubZeroDimAsync(mon.Id, value, SubZeroDimPercent);
        }
    }

    partial void OnSubZeroDimPercentChanged(int value)
    {
        if (_isSyncing) return;
        var mon = _displayService.ActiveMonitor;
        if (mon != null && IsSubZeroDimEnabled)
        {
            _ = _displayService.SetSubZeroDimAsync(mon.Id, true, value);
        }
    }

    partial void OnMasterVolumeChanged(int value)
    {
        if (_isSyncing) return;
        _ = _audioService.SetVolumeAsync(value);
    }

    partial void OnIsCompactModeChanged(bool value)
    {
        CompactModeChanged?.Invoke();
    }

    [RelayCommand]
    public void ToggleCompactMode()
    {
        IsCompactMode = !IsCompactMode;
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
        var dev = _audioService.ActiveDevice;
        if (dev != null)
        {
            ActiveDeviceName = dev.Name;
            MasterVolume = dev.VolumePercent;
            IsMuted = dev.IsMuted;
        }
    }

    [RelayCommand]
    public async Task ApplyEnvironment(EnvironmentPreset preset)
    {
        if (preset == null) return;
        var res = await _environmentService.ApplyEnvironmentAsync(preset.Id, _displayService, _audioService, _apoBridge);
        if (res.IsSuccess && res.Value != null)
        {
            ActiveEnvironmentName = res.Value.EnvironmentName;
            FeedbackMessage = res.Value.FeedbackMessage;
            SyncState();
        }
    }

    [RelayCommand]
    public async Task SelectEnvironmentByIndex(int index)
    {
        if (index >= 0 && index < Presets.Count)
        {
            await ApplyEnvironment(Presets[index]);
        }
    }

    [RelayCommand]
    public void OpenDisplayStudio()
    {
        RequestClose?.Invoke();
        _navigateAction("Display");
    }

    [RelayCommand]
    public void OpenAudioStudio()
    {
        RequestClose?.Invoke();
        _navigateAction("Audio");
    }

    [RelayCommand]
    public void CloseFlyout()
    {
        RequestClose?.Invoke();
    }
}
