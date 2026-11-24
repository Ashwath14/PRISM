using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PRISM.Core.Interfaces;
using PRISM.Core.Models;
using PRISM.Hardware.Audio;

namespace PRISM.App.ViewModels;

public partial class EnvironmentsViewModel : ObservableObject
{
    private readonly IEnvironmentService _environmentService;
    private readonly IDisplayService _displayService;
    private readonly IAudioService _audioService;
    private readonly IEqualizerApoBridge _apoBridge;
    private readonly IProfileService _profileService;

    public ObservableCollection<EnvironmentPreset> Presets { get; } = new();

    [ObservableProperty] private EnvironmentPreset? _selectedPreset;
    [ObservableProperty] private EnvironmentPreset? _previewedPreset;
    [ObservableProperty] private string _previewMiniCurvePath = "";
    [ObservableProperty] private bool _isPreviewingDifferent = false;
    [ObservableProperty] private string _statusFeedback = "Ready";
    [ObservableProperty] private bool _isPartialStatus = false;
    [ObservableProperty] private string _newEnvironmentName = "";
    [ObservableProperty] private bool _isCreatingNewEnvironment = false;

    // Day/Night Automation & Schedule
    private bool _isLoadingSchedule;
    [ObservableProperty] private bool _isScheduleEnabled = false;
    [ObservableProperty] private string _dayStartTimeText = "07:00";
    [ObservableProperty] private string _nightStartTimeText = "20:00";
    [ObservableProperty] private string _dayPresetName = "Daylight Focus";
    [ObservableProperty] private string _nightPresetName = "Late Night";
    [ObservableProperty] private bool _pauseDuringFullscreen = true;
    [ObservableProperty] private bool _isManualOverrideActive = false;
    [ObservableProperty] private string _manualOverrideStatus = "Schedule Synchronized";

    public event EventHandler<EnvironmentApplicationResult>? EnvironmentApplied;

    public EnvironmentsViewModel(
        IEnvironmentService environmentService,
        IDisplayService displayService,
        IAudioService audioService,
        IEqualizerApoBridge apoBridge,
        IProfileService profileService)
    {
        _environmentService = environmentService;
        _displayService = displayService;
        _audioService = audioService;
        _apoBridge = apoBridge;
        _profileService = profileService;

        LoadPresets();
        LoadSchedule();

        _environmentService.EnvironmentApplied += (s, res) =>
        {
            StatusFeedback = res.FeedbackMessage;
            IsPartialStatus = res.IsPartial;
            foreach (var p in Presets)
            {
                p.IsActive = (p.Name == res.EnvironmentName);
            }
            SelectedPreset = Presets.FirstOrDefault(p => p.Name == res.EnvironmentName) ?? SelectedPreset;
            PreviewedPreset = SelectedPreset;
            if (PreviewedPreset?.AudioSettings != null)
            {
                PreviewMiniCurvePath = AudioResponseCalculator.GenerateMiniSvgPath(PreviewedPreset.AudioSettings, 120, 32);
            }
            IsPreviewingDifferent = false;
            EnvironmentApplied?.Invoke(this, res);
        };
    }

    private void LoadSchedule()
    {
        _isLoadingSchedule = true;
        try
        {
            var s = _environmentService.GetSchedule();
            IsScheduleEnabled = s.IsEnabled;
            DayStartTimeText = s.DayStartTime.ToString(@"hh\:mm");
            NightStartTimeText = s.NightStartTime.ToString(@"hh\:mm");
            DayPresetName = s.DayEnvironmentName;
            NightPresetName = s.NightEnvironmentName;
            PauseDuringFullscreen = s.PauseDuringFullscreenMedia;
            IsManualOverrideActive = s.IsManualOverrideActive;
            UpdateOverrideStatus();
        }
        finally
        {
            _isLoadingSchedule = false;
        }
    }

    private void UpdateOverrideStatus()
    {
        if (!IsScheduleEnabled)
        {
            ManualOverrideStatus = "Automation Disabled (Manual Selection Mode)";
        }
        else if (IsManualOverrideActive)
        {
            ManualOverrideStatus = "Manual Override Active — Auto-switch paused until resume";
        }
        else
        {
            ManualOverrideStatus = "Schedule Active — Synchronized to local time";
        }
    }

    partial void OnIsScheduleEnabledChanged(bool value) => SaveScheduleSettings();
    partial void OnDayStartTimeTextChanged(string value) => SaveScheduleSettings();
    partial void OnNightStartTimeTextChanged(string value) => SaveScheduleSettings();
    partial void OnDayPresetNameChanged(string value) => SaveScheduleSettings();
    partial void OnNightPresetNameChanged(string value) => SaveScheduleSettings();
    partial void OnPauseDuringFullscreenChanged(bool value) => SaveScheduleSettings();

    private void SaveScheduleSettings()
    {
        if (_isLoadingSchedule) return;
        var s = _environmentService.GetSchedule();
        s.IsEnabled = IsScheduleEnabled;
        if (TimeSpan.TryParse(DayStartTimeText, out var dayTime)) s.DayStartTime = dayTime;
        if (TimeSpan.TryParse(NightStartTimeText, out var nightTime)) s.NightStartTime = nightTime;
        s.DayEnvironmentName = DayPresetName;
        s.NightEnvironmentName = NightPresetName;
        s.PauseDuringFullscreenMedia = PauseDuringFullscreen;
        s.IsManualOverrideActive = IsManualOverrideActive;
        _ = _environmentService.SaveScheduleAsync(s);
        UpdateOverrideStatus();
    }

    public void LoadPresets()
    {
        Presets.Clear();
        foreach (var p in _environmentService.GetPresets())
        {
            Presets.Add(p);
            if (p.IsActive) SelectedPreset = p;
        }

        if (SelectedPreset == null)
        {
            SelectedPreset = Presets.FirstOrDefault();
        }

        PreviewedPreset = SelectedPreset;
        if (PreviewedPreset?.AudioSettings != null)
        {
            PreviewMiniCurvePath = AudioResponseCalculator.GenerateMiniSvgPath(PreviewedPreset.AudioSettings, 120, 32);
        }
        IsPreviewingDifferent = false;
    }

    [RelayCommand]
    public void PreviewEnvironment(EnvironmentPreset preset)
    {
        if (preset == null) return;
        PreviewedPreset = preset;
        if (preset.AudioSettings != null)
        {
            PreviewMiniCurvePath = AudioResponseCalculator.GenerateMiniSvgPath(preset.AudioSettings, 120, 32);
        }
        else
        {
            PreviewMiniCurvePath = AudioResponseCalculator.GenerateMiniSvgPath(new AudioProfile(), 120, 32);
        }
        IsPreviewingDifferent = (SelectedPreset == null || preset.Id != SelectedPreset.Id);
        StatusFeedback = $"Inspecting '{preset.Name}' settings (Luminance {preset.DisplaySettings?.Brightness ?? 0}%, {preset.AudioSettings?.Name ?? "Audio"}). Hardware unaffected until applied.";
        IsPartialStatus = false;
    }

    [RelayCommand]
    public async Task ApplyEnvironment(EnvironmentPreset preset)
    {
        if (preset == null) return;
        SelectedPreset = preset;
        PreviewedPreset = preset;
        if (preset.AudioSettings != null)
        {
            PreviewMiniCurvePath = AudioResponseCalculator.GenerateMiniSvgPath(preset.AudioSettings, 120, 32);
        }
        IsPreviewingDifferent = false;

        // Set manual override active so schedule does not immediately revert user choice
        _environmentService.SetManualOverride(true);
        IsManualOverrideActive = true;
        UpdateOverrideStatus();

        var result = await _environmentService.ApplyEnvironmentAsync(preset.Id, _displayService, _audioService, _apoBridge);
        if (result.IsSuccess && result.Value != null)
        {
            StatusFeedback = result.Value.FeedbackMessage;
            IsPartialStatus = result.Value.IsPartial;
            foreach (var p in Presets)
            {
                p.IsActive = (p.Id == preset.Id);
            }
        }
        else
        {
            StatusFeedback = result.ErrorMessage ?? "Failed to apply environment.";
            IsPartialStatus = true;
        }
    }

    [RelayCommand]
    public async Task ResumeSchedule()
    {
        _environmentService.SetManualOverride(false);
        IsManualOverrideActive = false;
        UpdateOverrideStatus();

        var schedule = _environmentService.GetSchedule();
        if (!schedule.IsEnabled) return;

        var now = DateTime.Now.TimeOfDay;
        string targetName;
        if (schedule.DayStartTime < schedule.NightStartTime)
        {
            bool isDay = now >= schedule.DayStartTime && now < schedule.NightStartTime;
            targetName = isDay ? schedule.DayEnvironmentName : schedule.NightEnvironmentName;
        }
        else
        {
            bool isDay = now >= schedule.DayStartTime || now < schedule.NightStartTime;
            targetName = isDay ? schedule.DayEnvironmentName : schedule.NightEnvironmentName;
        }

        var target = Presets.FirstOrDefault(p => p.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase));
        if (target != null)
        {
            await ApplyEnvironment(target);
            // Re-clear override after applying the scheduled environment
            _environmentService.SetManualOverride(false);
            IsManualOverrideActive = false;
            UpdateOverrideStatus();
            StatusFeedback = $"Schedule resumed. Applied '{targetName}'.";
        }
    }

    [RelayCommand]
    public async Task CreateEnvironmentFromCurrentState()
    {
        string name = string.IsNullOrWhiteSpace(NewEnvironmentName) 
            ? $"Custom Studio {Presets.Count + 1}" 
            : NewEnvironmentName.Trim();

        var mon = _displayService.ActiveMonitor;
        var displaySettings = new DisplayProfile
        {
            Name = $"{name} Display",
            Brightness = mon?.CurrentBrightness ?? 50,
            Contrast = mon?.CurrentContrast ?? 50,
            RedGain = mon?.RedGain ?? 50,
            GreenGain = mon?.GreenGain ?? 50,
            BlueGain = mon?.BlueGain ?? 50,
            IsSubZeroDimEnabled = mon?.IsSubZeroDimEnabled ?? false,
            SubZeroDimPercent = mon?.SubZeroDimPercent ?? 0
        };

        var dev = _audioService.ActiveDevice;
        var audioSettings = _profileService.GetAudioProfiles().FirstOrDefault()?.Clone() ?? new AudioProfile();
        audioSettings.Name = $"{name} Audio";

        var createResult = await _environmentService.CreateCustomEnvironmentAsync(
            name,
            $"Custom unified setup configured from live hardware state ({displaySettings.Brightness}% Brightness)",
            "SunIcon",
            displaySettings,
            audioSettings,
            dev?.Id);

        if (createResult.IsSuccess && createResult.Value != null)
        {
            Presets.Add(createResult.Value);
            SelectedPreset = createResult.Value;
            IsCreatingNewEnvironment = false;
            NewEnvironmentName = "";
            StatusFeedback = $"Environment '{name}' successfully created and saved.";
            IsPartialStatus = false;
        }
    }

    [RelayCommand]
    public async Task DuplicateEnvironment()
    {
        if (SelectedPreset == null) return;
        var result = await _environmentService.DuplicateEnvironmentAsync(SelectedPreset.Id);
        if (result.IsSuccess && result.Value != null)
        {
            Presets.Add(result.Value);
            SelectedPreset = result.Value;
            StatusFeedback = $"Duplicated '{result.Value.Name}'.";
            IsPartialStatus = false;
        }
    }

    [RelayCommand]
    public async Task DeleteEnvironment()
    {
        if (SelectedPreset == null || !SelectedPreset.IsCustom)
        {
            StatusFeedback = "Cannot delete built-in factory environment.";
            return;
        }

        var toDelete = SelectedPreset;
        var result = await _environmentService.DeleteEnvironmentAsync(toDelete.Id);
        if (result.IsSuccess)
        {
            Presets.Remove(toDelete);
            SelectedPreset = Presets.FirstOrDefault();
            StatusFeedback = $"Environment '{toDelete.Name}' removed.";
            IsPartialStatus = false;
        }
    }

    [RelayCommand]
    public async Task RestoreDefaultEnvironments()
    {
        var result = await _environmentService.RestoreDefaultEnvironmentsAsync();
        if (result.IsSuccess)
        {
            LoadPresets();
            StatusFeedback = "Factory default environments restored.";
            IsPartialStatus = false;
        }
    }
}
