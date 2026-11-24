using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PRISM.Core.Common;
using PRISM.Core.Models;

namespace PRISM.Core.Interfaces;

public interface IDisplayService
{
    IReadOnlyList<MonitorInfo> GetConnectedMonitors();
    MonitorInfo? ActiveMonitor { get; }
    void SelectMonitor(string monitorId);

    Task<Result> SetBrightnessAsync(string monitorId, int brightness);
    Task<Result> SetContrastAsync(string monitorId, int contrast);
    Task<Result> SetRgbGainsAsync(string monitorId, int red, int green, int blue);
    Task<Result> SetInputSourceAsync(string monitorId, string inputSource);
    Task<Result> SetSubZeroDimAsync(string monitorId, bool enabled, int percent);

    Task<Result<int>> RefreshHardwareBrightnessAsync(string monitorId);
    Task<Result> RefreshAllHardwareValuesAsync(string monitorId);

    Task<Result> ApplyDisplayProfileAsync(string monitorId, DisplayProfile profile);

    void HandleTopologyChange();
    Task<Result> SaveCurrentPreferencesForActiveMonitorAsync(string? profileName = null);

    event EventHandler<MonitorInfo>? DisplayConfigChanged;
    event EventHandler<HardwareStatus>? HardwareStatusUpdated;
    event EventHandler<MonitorInfo>? MonitorConnected;
    event EventHandler<string>? MonitorDisconnected;
}

public interface ISoftwareDimmerService : IDisposable
{
    void SetDimming(int screenX, int screenY, int screenWidth, int screenHeight, bool enabled, int percent);
    void ClearAll();
}

public interface IAudioService
{
    IReadOnlyList<AudioDeviceInfo> GetConnectedDevices();
    AudioDeviceInfo? ActiveDevice { get; }
    
    Task<Result> SetActiveDeviceAsync(string deviceId);
    Task<Result> SetVolumeAsync(int volumePercent);
    Task<Result> SetMuteAsync(bool isMuted);
    Task<Result> ToggleOutputDeviceAsync();
    Task<Result<(float Left, float Right)>> GetChannelBalanceAsync(string? deviceId = null);
    Task<Result> SetChannelBalanceAsync(float left, float right, string? deviceId = null);

    event EventHandler<AudioDeviceInfo>? AudioEndpointChanged;
}

public interface IEqualizerApoBridge
{
    EqualizerApoStatus CheckStatus(string? targetDeviceName = null);
    Task<Result> ApplyProfileAsync(AudioProfile profile, string? targetDeviceName = null);
    string GenerateConfigString(AudioProfile profile, string? targetDeviceName = null);
    Result ValidateConfigSyntax(string configContent);
    Task<Result<string>> ExportConfigAsync(AudioProfile profile, string destinationPath);
    Task<Result> BackupConfigurationAsync();
    Task<Result> RestoreBackupConfigurationAsync();
}

public interface IProfileService
{
    IReadOnlyList<DisplayProfile> GetDisplayProfiles();
    IReadOnlyList<AudioProfile> GetAudioProfiles();
    Task<Result> SaveDisplayProfileAsync(DisplayProfile profile);
    Task<Result> SaveAudioProfileAsync(AudioProfile profile);
    Task<Result> DeleteDisplayProfileAsync(string profileId);
    Task<Result> DeleteAudioProfileAsync(string profileId);

    Task<Result<DisplayProfile>> CreateCustomDisplayProfileAsync(string name, int brightness, int contrast, int red, int green, int blue, bool subZero, int subZeroPct);
    Task<Result<DisplayProfile>> DuplicateDisplayProfileAsync(string profileId);
    Task<Result> RenameDisplayProfileAsync(string profileId, string newName);

    Task<Result<AudioProfile>> CreateCustomAudioProfileAsync(string name, double[] bandGains, double bassGain, double trebleGain, string category = "Custom");
    Task<Result<AudioProfile>> DuplicateAudioProfileAsync(string profileId);
    Task<Result> RenameAudioProfileAsync(string profileId, string newName);

    Task<Result> RestoreDefaultProfilesAsync();

    // Per-monitor preferences
    MonitorPreference? GetMonitorPreference(string monitorKey);
    Task<Result> SaveMonitorPreferenceAsync(MonitorPreference preference);

    // Audio preset import/export
    Task<Result<string>> ExportAudioPresetAsync(string profileId, string filePath);
    Task<Result<AudioProfile>> ImportAudioPresetAsync(string filePath);

    // Display profile import/export
    Task<Result<string>> ExportDisplayProfileAsync(string profileId, string filePath);
    Task<Result<DisplayProfile>> ImportDisplayProfileAsync(string filePath);

    // Full system backup & restore
    Task<Result<string>> CreateFullBackupAsync(string destinationPath);
    Task<Result> RestoreFullBackupAsync(string sourcePath);
    Task<Result> ResetModuleAsync(string moduleName);

    // Headphone AutoEq calibration profiles
    IReadOnlyList<HeadphoneProfile> GetHeadphoneProfiles();
    Task<Result> SaveHeadphoneProfileAsync(HeadphoneProfile profile);
    Task<Result<HeadphoneProfile>> ImportAutoEqProfileAsync(string filePath);
    Task<Result> DeleteHeadphoneProfileAsync(string profileId);
}

public interface IDisplayResolutionService
{
    IReadOnlyList<DisplayModeInfo> GetSupportedModes(string? deviceName = null);
    DisplayModeInfo? GetCurrentMode(string? deviceName = null);
    DisplayModeInfo? GetOriginalMode(string? deviceName = null);
    Task<Result> ApplyModeAsync(DisplayModeInfo mode, string? deviceName = null);
    Task<Result> RevertModeAsync(string? deviceName = null);
    bool IsRollbackPending { get; }
    void ConfirmAppliedMode();
}

public interface IDisplayAdvancedColorService
{
    DisplayHdrInfo GetAdvancedColorInfo(string? deviceName = null);
    Task<Result> SetHdrEnabledAsync(bool enabled, string? deviceName = null);
}

public interface ISpatialAudioService
{
    SpatialAudioInfo GetSpatialAudioInfo(string? deviceId = null);
    void OpenWindowsSoundSettings();
    void OpenDolbyAccess();
}

public interface IEnvironmentService
{
    IReadOnlyList<EnvironmentPreset> GetPresets();
    EnvironmentPreset? ActivePreset { get; }
    
    Task<Result<EnvironmentApplicationResult>> ApplyEnvironmentAsync(
        string presetId,
        IDisplayService displayService,
        IAudioService audioService,
        IEqualizerApoBridge apoBridge);

    Task<Result<EnvironmentPreset>> CreateCustomEnvironmentAsync(
        string name,
        string description,
        string iconKey,
        DisplayProfile display,
        AudioProfile audio,
        string? targetAudioDeviceId = null);

    Task<Result<EnvironmentPreset>> DuplicateEnvironmentAsync(string presetId);
    Task<Result> RenameEnvironmentAsync(string presetId, string newName);
    Task<Result> UpdateEnvironmentAsync(EnvironmentPreset preset);
    Task<Result> DeleteEnvironmentAsync(string presetId);
    Task<Result> RestoreDefaultEnvironmentsAsync();

    AppSettings GetSettings();
    Task<Result> SaveSettingsAsync(AppSettings settings);

    EnvironmentSchedule GetSchedule();
    Task<Result> SaveScheduleAsync(EnvironmentSchedule schedule);
    void SetManualOverride(bool isOverridden);

    event EventHandler<EnvironmentApplicationResult>? EnvironmentApplied;
}
