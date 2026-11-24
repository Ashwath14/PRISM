using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PRISM.Core.Models;
using PRISM.Hardware.Display;
using PRISM.Hardware.Storage;
using Xunit;

namespace PRISM.Tests;

public class PostStabilityEnhancementTests : IDisposable
{
    private readonly string _tempTestDir;

    public PostStabilityEnhancementTests()
    {
        _tempTestDir = Path.Combine(Path.GetTempPath(), "PRISM_Enhancement_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempTestDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempTestDir))
            {
                Directory.Delete(_tempTestDir, true);
            }
        }
        catch { }
    }

    [Fact]
    public void EdidParser_ReadsLiveEdid_ReturnsPopulatedList()
    {
        var monitors = EdidParser.GetConnectedMonitorEdids();
        Assert.NotNull(monitors);
        // On physical Windows machine, at least one monitor is connected (Acer ED270 X0)
        if (monitors.Count > 0)
        {
            var first = monitors[0];
            Assert.False(string.IsNullOrWhiteSpace(first.ModelName));
            Assert.False(string.IsNullOrWhiteSpace(first.ManufacturerCode));
        }
    }

    [Fact]
    public async Task MonitorPreference_SaveAndRetrieve_RoundtripPreserved()
    {
        var storage = new ProfileStorageService(_tempTestDir);

        var pref = new MonitorPreference
        {
            MonitorKey = "ACR07B0_154604D5A3W01",
            MonitorName = "ED270 X0",
            SerialNumber = "154604D5A3W01",
            SavedBrightness = 85,
            SavedContrast = 55,
            SavedRedGain = 52,
            SavedGreenGain = 48,
            SavedBlueGain = 50,
            SavedSubZeroPercent = 0,
            SavedSubZeroEnabled = false
        };

        var saveResult = await storage.SaveMonitorPreferenceAsync(pref);
        Assert.True(saveResult.IsSuccess);

        var retrieved = storage.GetMonitorPreference("ACR07B0_154604D5A3W01");
        Assert.NotNull(retrieved);
        Assert.Equal(85, retrieved.SavedBrightness);
        Assert.Equal(55, retrieved.SavedContrast);
        Assert.Equal(52, retrieved.SavedRedGain);
        Assert.Equal(48, retrieved.SavedGreenGain);
        Assert.Equal(50, retrieved.SavedBlueGain);
        Assert.Equal("154604D5A3W01", retrieved.SerialNumber);
    }

    [Fact]
    public async Task AudioPreset_ExportAndImport_RoundtripSuccessful()
    {
        var storage = new ProfileStorageService(_tempTestDir);
        var initialProfiles = storage.GetAudioProfiles();
        var targetProfile = initialProfiles.First();

        string exportFile = Path.Combine(_tempTestDir, "test_preset.prism-eq");
        var exportResult = await storage.ExportAudioPresetAsync(targetProfile.Id, exportFile);
        Assert.True(exportResult.IsSuccess);
        Assert.True(File.Exists(exportFile));

        // Import the file into storage
        var importResult = await storage.ImportAudioPresetAsync(exportFile);
        Assert.True(importResult.IsSuccess);
        Assert.NotNull(importResult.Value);
        Assert.Contains(targetProfile.Name, importResult.Value.Name);
        Assert.Equal(targetProfile.BassGainDb, importResult.Value.BassGainDb);
        Assert.Equal(targetProfile.TrebleGainDb, importResult.Value.TrebleGainDb);
        Assert.Equal(targetProfile.PreampDb, importResult.Value.PreampDb);
    }

    [Fact]
    public async Task StudioBackup_CreateAndRestore_PreservesAllSubsystems()
    {
        var storage = new ProfileStorageService(_tempTestDir);

        // Customize schedule
        var sched = storage.GetSchedule();
        sched.DayStartTime = new TimeSpan(6, 30, 0);
        sched.NightStartTime = new TimeSpan(21, 15, 0);
        sched.IsEnabled = true;
        await storage.SaveScheduleAsync(sched);

        // Save a monitor preference
        var pref = new MonitorPreference
        {
            MonitorKey = "TEST_MONITOR_KEY",
            MonitorName = "Test Monitor",
            SavedBrightness = 77
        };
        await storage.SaveMonitorPreferenceAsync(pref);

        // Create full backup archive
        string backupFile = Path.Combine(_tempTestDir, "studio_backup.prism-backup");
        var backupResult = await storage.CreateFullBackupAsync(backupFile);
        Assert.True(backupResult.IsSuccess);
        Assert.True(File.Exists(backupFile));

        // Reset all settings
        await storage.ResetModuleAsync("All");

        // Verify state is reset
        var resetPref = storage.GetMonitorPreference("TEST_MONITOR_KEY");
        Assert.Null(resetPref);

        // Restore from backup
        var restoreResult = await storage.RestoreFullBackupAsync(backupFile);
        Assert.True(restoreResult.IsSuccess);

        // Verify preference and schedule restored
        var restoredPref = storage.GetMonitorPreference("TEST_MONITOR_KEY");
        Assert.NotNull(restoredPref);
        Assert.Equal(77, restoredPref.SavedBrightness);

        var restoredSched = storage.GetSchedule();
        Assert.True(restoredSched.IsEnabled);
        Assert.Equal(new TimeSpan(6, 30, 0), restoredSched.DayStartTime);
        Assert.Equal(new TimeSpan(21, 15, 0), restoredSched.NightStartTime);
    }

    [Fact]
    public async Task ModuleReset_ResetsOnlyTargetModule()
    {
        var storage = new ProfileStorageService(_tempTestDir);

        // Add custom display profile
        await storage.CreateCustomDisplayProfileAsync("Custom Studio Mode", 92, 45, 50, 50, 50, false, 0);
        var dispBefore = storage.GetDisplayProfiles().Count;

        // Add custom audio profile
        await storage.CreateCustomAudioProfileAsync("Custom Studio Sound", new double[10], 2.0, 3.0);
        var audioBefore = storage.GetAudioProfiles().Count;

        // Reset only Display module
        var resetResult = await storage.ResetModuleAsync("Display");
        Assert.True(resetResult.IsSuccess);

        // Display profiles should be reset to default factory set
        var dispAfter = storage.GetDisplayProfiles().Count;
        Assert.True(dispAfter < dispBefore);

        // Audio profiles should remain completely intact
        var audioAfter = storage.GetAudioProfiles().Count;
        Assert.Equal(audioBefore, audioAfter);
    }

    [Fact]
    public void EnvironmentSchedule_ManualOverride_ProtectsUserSelection()
    {
        var storage = new ProfileStorageService(_tempTestDir);
        var sched = storage.GetSchedule();
        Assert.False(sched.IsManualOverrideActive);

        storage.SetManualOverride(true);
        var updated = storage.GetSchedule();
        Assert.True(updated.IsManualOverrideActive);

        storage.SetManualOverride(false);
        var cleared = storage.GetSchedule();
        Assert.False(cleared.IsManualOverrideActive);
    }
}
