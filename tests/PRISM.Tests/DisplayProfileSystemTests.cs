using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PRISM.App.ViewModels;
using PRISM.Core.Models;
using PRISM.Hardware.Display;
using PRISM.Hardware.Storage;
using Xunit;

namespace PRISM.Tests;

public class DisplayProfileSystemTests : IDisposable
{
    private readonly string _tempTestDir;

    public DisplayProfileSystemTests()
    {
        _tempTestDir = Path.Combine(Path.GetTempPath(), "PRISM_DisplayProfileTests_" + Guid.NewGuid().ToString("N"));
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
    public void StorageService_FactoryProfiles_ContainsTheFourRequiredProfiles()
    {
        var storage = new ProfileStorageService(_tempTestDir);
        var profiles = storage.GetDisplayProfiles();

        Assert.NotNull(profiles);
        Assert.Equal(4, profiles.Count);

        var daylight = profiles.FirstOrDefault(p => p.Id == "disp_daylight");
        Assert.NotNull(daylight);
        Assert.Equal("Daylight Focus", daylight.Name);
        Assert.Equal("Productivity", daylight.Category);
        Assert.Equal(55, daylight.Brightness);
        Assert.Equal(50, daylight.Contrast);
        Assert.Equal(50, daylight.RedGain);
        Assert.Equal(50, daylight.GreenGain);
        Assert.Equal(50, daylight.BlueGain);
        Assert.True(daylight.IsBuiltIn);
        Assert.False(daylight.IsCustom);

        var night = profiles.FirstOrDefault(p => p.Id == "disp_latenight");
        Assert.NotNull(night);
        Assert.Equal("Late Night", night.Name);
        Assert.Equal("Relaxation", night.Category);
        Assert.Equal(25, night.Brightness);
        Assert.Equal(48, night.Contrast);
        Assert.True(night.RedGain > night.BlueGain, "Late Night must have warmer red gain than blue gain");
        Assert.True(night.IsBuiltIn);

        var gaming = profiles.FirstOrDefault(p => p.Id == "disp_gaming");
        Assert.NotNull(gaming);
        Assert.Equal("Competitive Gaming", gaming.Name);
        Assert.Equal("Gaming", gaming.Category);
        Assert.Equal(75, gaming.Brightness);
        Assert.True(gaming.Brightness > 50, "Competitive Gaming must NEVER reduce brightness to 0% or low levels");
        Assert.Equal(52, gaming.Contrast);
        Assert.True(gaming.IsBuiltIn);

        var cinema = profiles.FirstOrDefault(p => p.Id == "disp_cinema");
        Assert.NotNull(cinema);
        Assert.Equal("Cinematic Movie", cinema.Name);
        Assert.Equal("Cinema", cinema.Category);
        Assert.Equal(60, cinema.Brightness);
        Assert.Equal(55, cinema.Contrast);
        Assert.True(cinema.IsBuiltIn);
    }

    [Fact]
    public void DisplayProfile_HonestCapabilities_AccuratelyReportsHardwareSupport()
    {
        var profile = new DisplayProfile
        {
            Name = "Audit Profile",
            Brightness = 65,
            Contrast = 50,
            RedGain = 50,
            GreenGain = 50,
            BlueGain = 50
        };

        // Hardware supported on Acer ED270 X0
        Assert.Equal(DisplayCapabilityStatus.Supported, profile.BrightnessCapability.Status);
        Assert.Equal(DisplayCapabilityStatus.Supported, profile.ContrastCapability.Status);
        Assert.Equal(DisplayCapabilityStatus.Supported, profile.RgbGainCapability.Status);
        Assert.Equal(DisplayCapabilityStatus.Supported, profile.ColorTemperatureCapability.Status);

        // Honest reporting: unexposed or unsupported scaler properties
        Assert.Equal(DisplayCapabilityStatus.NotConfigured, profile.SharpnessCapability.Status);
        Assert.Contains("Native", profile.SharpnessCapability.DisplayText);

        Assert.Equal(DisplayCapabilityStatus.Unsupported, profile.SaturationCapability.Status);
        Assert.Contains("Not supported", profile.SaturationCapability.DisplayText);

        Assert.Equal(DisplayCapabilityStatus.NotConfigured, profile.GammaCapability.Status);
        Assert.Contains("2.2", profile.GammaCapability.DisplayText);
    }

    [Fact]
    public async Task DisplayProfile_CannotDeleteFactoryProfiles()
    {
        var storage = new ProfileStorageService(_tempTestDir);

        var res = await storage.DeleteDisplayProfileAsync("disp_gaming");
        Assert.False(res.IsSuccess);
        Assert.Contains("Factory profiles cannot be deleted", res.Error);

        var profiles = storage.GetDisplayProfiles();
        Assert.Contains(profiles, p => p.Id == "disp_gaming");
    }

    [Fact]
    public async Task DisplayProfile_CustomCRUD_WorksAndPersists()
    {
        var storage = new ProfileStorageService(_tempTestDir);

        // 1. Create custom profile
        var createRes = await storage.CreateCustomDisplayProfileAsync("My Custom Grade", 62, 54, 52, 50, 48, false, 0);
        Assert.True(createRes.IsSuccess);
        var created = createRes.Value!;
        Assert.Equal("My Custom Grade", created.Name);
        Assert.True(created.IsCustom);
        Assert.False(created.IsBuiltIn);

        // 2. Duplicate custom profile
        var dupRes = await storage.DuplicateDisplayProfileAsync(created.Id);
        Assert.True(dupRes.IsSuccess);
        var duplicated = dupRes.Value!;
        Assert.NotEqual(created.Id, duplicated.Id);
        Assert.Contains("Copy", duplicated.Name);
        Assert.True(duplicated.IsCustom);

        // 3. Rename profile
        var renameRes = await storage.RenameDisplayProfileAsync(duplicated.Id, "Renamed Custom Profile");
        Assert.True(renameRes.IsSuccess);

        // 4. Delete duplicated profile
        var delRes = await storage.DeleteDisplayProfileAsync(duplicated.Id);
        Assert.True(delRes.IsSuccess);

        // 5. Verify persistence across new instance from same directory
        var storage2 = new ProfileStorageService(_tempTestDir);
        var loadedProfiles = storage2.GetDisplayProfiles();
        Assert.Contains(loadedProfiles, p => p.Id == created.Id);
        Assert.DoesNotContain(loadedProfiles, p => p.Id == duplicated.Id);
    }

    [Fact]
    public async Task DisplayProfile_ExportAndImport_RoundtripsSuccessfully()
    {
        var storage = new ProfileStorageService(_tempTestDir);
        var exportPath = Path.Combine(_tempTestDir, "daylight_focus.prism-disp");

        var exportRes = await storage.ExportDisplayProfileAsync("disp_daylight", exportPath);
        Assert.True(exportRes.IsSuccess);
        Assert.True(File.Exists(exportPath));

        var importRes = await storage.ImportDisplayProfileAsync(exportPath);
        Assert.True(importRes.IsSuccess);
        var imported = importRes.Value!;

        Assert.NotEqual("disp_daylight", imported.Id);
        Assert.Contains("Daylight Focus", imported.Name);
        Assert.True(imported.IsCustom);
        Assert.Equal(55, imported.Brightness);
        Assert.Equal(50, imported.Contrast);
    }

    [Fact]
    public async Task DisplayService_ApplyDisplayProfile_AppliesSequentiallyAndVerifies()
    {
        var displayService = new DdcCiMonitorService();
        var profile = new DisplayProfile
        {
            Id = "disp_test",
            Name = "Test Verification Profile",
            Brightness = 58,
            Contrast = 52,
            RedGain = 50,
            GreenGain = 50,
            BlueGain = 50
        };

        var res = await displayService.ApplyDisplayProfileAsync("MONITOR_1", profile);
        Assert.True(res.IsSuccess);

        var active = displayService.ActiveMonitor;
        Assert.NotNull(active);
        Assert.Equal(58, active.CurrentBrightness);
        Assert.Equal(52, active.CurrentContrast);
        Assert.Equal(50, active.RedGain);
    }

    [Fact]
    public async Task DisplayStudioViewModel_ProfileSelection_UpdatesSelectedProfileAndInspectState()
    {
        var storage = new ProfileStorageService(_tempTestDir);
        var displayService = new DdcCiMonitorService();
        var vm = new DisplayStudioViewModel(displayService, storage);

        Assert.NotEmpty(vm.Profiles);
        var gamingProfile = vm.Profiles.First(p => p.Id == "disp_gaming");

        vm.SelectProfile(gamingProfile);
        Assert.Equal(gamingProfile, vm.SelectedProfile);
        Assert.Equal(75, vm.SelectedProfile.Brightness);

        // Apply profile
        await vm.ApplySelectedProfile();
        Assert.Equal(gamingProfile, vm.ActiveAppliedProfile);
        Assert.Equal("Competitive Gaming", vm.ActiveProfileName);
        Assert.Equal(75, vm.Brightness);
        Assert.Equal(52, vm.Contrast);
        Assert.False(vm.IsProfileModified);

        // Move slider manually -> triggers modified flag
        vm.Brightness = 82;
        Assert.True(vm.IsProfileModified);
        Assert.Contains("Competitive Gaming", vm.ProfileModifiedWarning);

        // Reapply active profile -> restores values and clears modified flag
        await vm.ReapplyActiveProfile();
        Assert.Equal(75, vm.Brightness);
        Assert.False(vm.IsProfileModified);
    }

    [Fact]
    public async Task LiveHardware_AcerED270X0_FullProfileWorkflow_Verification()
    {
        var displayService = new DdcCiMonitorService();
        var storage = new ProfileStorageService(_tempTestDir);
        var activeMon = displayService.ActiveMonitor;

        if (activeMon == null || !activeMon.IsHardwareDdcCi)
        {
            // Skip physical hardware assertions if in CI/mock environment without DDC/CI monitor
            return;
        }

        // 1. Initial State Read
        int initialBr = activeMon.CurrentBrightness;
        int initialCt = activeMon.CurrentContrast;

        var vm = new DisplayStudioViewModel(displayService, storage);

        // 2. Test Selection without Applying
        var gaming = vm.Profiles.First(p => p.Id == "disp_gaming");
        vm.SelectProfile(gaming);
        Assert.Equal(gaming, vm.SelectedProfile);
        Assert.NotEqual(gaming.Name, vm.ActiveProfileName); // Active has NOT changed

        // Check physical monitor brightness did not change merely by selecting
        var brCheck = await displayService.RefreshHardwareBrightnessAsync(activeMon.Id);
        Assert.True(brCheck.IsSuccess);
        Assert.Equal(initialBr, brCheck.Value);

        // 3. Apply Profile 1: Daylight Focus (55% Br, 50% Ct, RGB 50/50/50)
        var daylight = vm.Profiles.First(p => p.Id == "disp_daylight");
        vm.SelectProfile(daylight);
        await vm.ApplySelectedProfile();
        Assert.Equal("Daylight Focus", vm.ActiveProfileName);
        Assert.Equal(55, vm.Brightness);
        Assert.Equal(50, vm.Contrast);
        Assert.Equal(50, vm.RedGain);
        Assert.False(vm.IsProfileModified);

        // Readback directly from hardware
        await Task.Delay(450);
        await displayService.RefreshAllHardwareValuesAsync(activeMon.Id);
        Assert.Equal(55, activeMon.CurrentBrightness);
        Assert.Equal(50, activeMon.CurrentContrast);
        Assert.Equal(50, activeMon.RedGain);
        Assert.Equal(50, activeMon.GreenGain);
        Assert.Equal(50, activeMon.BlueGain);

        // 4. Apply Profile 2: Late Night (25% Br, 48% Ct, RGB 52/50/46)
        var night = vm.Profiles.First(p => p.Id == "disp_latenight");
        vm.SelectProfile(night);
        await vm.ApplySelectedProfile();
        Assert.Equal("Late Night", vm.ActiveProfileName);
        Assert.Equal(25, vm.Brightness);
        Assert.Equal(48, vm.Contrast);

        await Task.Delay(650);
        await displayService.RefreshAllHardwareValuesAsync(activeMon.Id);
        Assert.Equal(25, activeMon.CurrentBrightness);
        Assert.Equal(48, activeMon.CurrentContrast);
        Assert.Equal(52, activeMon.RedGain);
        Assert.Equal(50, activeMon.GreenGain);
        Assert.Equal(46, activeMon.BlueGain);

        // 5. Apply Profile 3: Competitive Gaming (75% Br, 52% Ct, RGB 50/50/50)
        vm.SelectProfile(gaming);
        await vm.ApplySelectedProfile();
        Assert.Equal("Competitive Gaming", vm.ActiveProfileName);
        Assert.Equal(75, vm.Brightness);
        Assert.True(vm.Brightness > 50, "Competitive Gaming must NEVER reduce brightness to 0%!");

        await Task.Delay(650);
        await displayService.RefreshAllHardwareValuesAsync(activeMon.Id);
        Assert.Equal(75, activeMon.CurrentBrightness);
        Assert.Equal(52, activeMon.CurrentContrast);
        Assert.Equal(50, activeMon.RedGain);

        // 6. Apply Profile 4: Cinematic Movie (60% Br, 55% Ct, RGB 52/50/48)
        var cinema = vm.Profiles.First(p => p.Id == "disp_cinema");
        vm.SelectProfile(cinema);
        await vm.ApplySelectedProfile();
        Assert.Equal("Cinematic Movie", vm.ActiveProfileName);
        Assert.Equal(60, vm.Brightness);
        Assert.Equal(55, vm.Contrast);

        await Task.Delay(650);
        await displayService.RefreshAllHardwareValuesAsync(activeMon.Id);
        Assert.Equal(60, activeMon.CurrentBrightness);
        Assert.Equal(55, activeMon.CurrentContrast);
        Assert.Equal(52, activeMon.RedGain);
        Assert.Equal(50, activeMon.GreenGain);
        Assert.Equal(48, activeMon.BlueGain);

        // 7. Test Modified State & Revert
        vm.Brightness = 70;
        Assert.True(vm.IsProfileModified);
        Assert.Contains("Cinematic Movie", vm.ProfileModifiedWarning);

        await vm.ReapplyActiveProfile();
        Assert.Equal(60, vm.Brightness);
        Assert.False(vm.IsProfileModified);

        // 8. Restore baseline to comfortable 55% Daylight Focus
        await displayService.ApplyDisplayProfileAsync(activeMon.Id, daylight);
        await Task.Delay(200);
    }
}
