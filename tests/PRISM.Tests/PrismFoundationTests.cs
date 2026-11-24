using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PRISM.App.Services;
using PRISM.App.ViewModels;
using PRISM.Core.Models;
using PRISM.Hardware.Audio;
using PRISM.Hardware.Display;
using PRISM.Hardware.Storage;
using PRISM.Hardware.Windows;
using Xunit;

namespace PRISM.Tests;

public class PrismFoundationTests
{
    [Fact]
    public void DisplayService_DiscoversMonitors_AndIdentifiesAcerED270X0()
    {
        var displayService = new DdcCiMonitorService();
        var monitors = displayService.GetConnectedMonitors();

        Assert.NotEmpty(monitors);
        var active = displayService.ActiveMonitor;
        Assert.NotNull(active);
        Assert.Contains("ED270", active.Name);
        Assert.True(active.Capabilities.IsDdcCiSupported);
    }

    [Fact]
    public async Task DisplayService_ClampsBrightness_WithinValidRange()
    {
        var displayService = new DdcCiMonitorService();
        var active = displayService.ActiveMonitor!;
        int initial = active.CurrentBrightness;

        await displayService.SetBrightnessAsync(active.Id, 150);
        Assert.Equal(100, active.CurrentBrightness);

        await displayService.SetBrightnessAsync(active.Id, -20);
        Assert.Equal(0, active.CurrentBrightness);

        await displayService.SetBrightnessAsync(active.Id, 44);
        Assert.Equal(44, active.CurrentBrightness);

        // Restore initial baseline
        await displayService.SetBrightnessAsync(active.Id, initial);
    }

    [Fact]
    public async Task DisplayProfiles_CreateDuplicateRenameDelete_WorksAndPersists()
    {
        var storage = new ProfileStorageService();
        
        // 1. Create custom
        var createResult = await storage.CreateCustomDisplayProfileAsync(
            "Studio Master", 65, 55, 52, 50, 48, false, 0);

        Assert.True(createResult.IsSuccess);
        var created = createResult.Value!;
        Assert.Equal("Studio Master", created.Name);
        Assert.Equal(65, created.Brightness);
        Assert.True(created.IsCustom);

        // 2. Duplicate
        var dupResult = await storage.DuplicateDisplayProfileAsync(created.Id);
        Assert.True(dupResult.IsSuccess);
        var duplicated = dupResult.Value!;
        Assert.Contains("Copy", duplicated.Name);
        Assert.Equal(65, duplicated.Brightness);

        // 3. Rename
        var renameResult = await storage.RenameDisplayProfileAsync(duplicated.Id, "Studio Master Renamed");
        Assert.True(renameResult.IsSuccess);
        var renamed = storage.GetDisplayProfiles().FirstOrDefault(p => p.Id == duplicated.Id);
        Assert.Equal("Studio Master Renamed", renamed?.Name);

        // 4. Delete
        var deleteResult = await storage.DeleteDisplayProfileAsync(created.Id);
        Assert.True(deleteResult.IsSuccess);
        Assert.DoesNotContain(storage.GetDisplayProfiles(), p => p.Id == created.Id);

        // Cleanup
        await storage.DeleteDisplayProfileAsync(duplicated.Id);
    }

    [Fact]
    public void DisplayStudioViewModel_GainPresets_AdjustsRgbChannelsAccurately()
    {
        var displayService = new DdcCiMonitorService();
        var storage = new ProfileStorageService();
        var vm = new DisplayStudioViewModel(displayService, storage);

        vm.SetWarmGainPreset();
        Assert.Equal(56, vm.RedGain);
        Assert.Equal(48, vm.GreenGain);
        Assert.Equal(40, vm.BlueGain);
        Assert.Contains("Warm", vm.ApproxColorProfileText);

        vm.SetCoolGainPreset();
        Assert.Equal(44, vm.RedGain);
        Assert.Equal(50, vm.GreenGain);
        Assert.Equal(56, vm.BlueGain);
        Assert.Contains("Cool", vm.ApproxColorProfileText);

        vm.SetNeutralGainPreset();
        Assert.Equal(50, vm.RedGain);
        Assert.Equal(50, vm.GreenGain);
        Assert.Equal(50, vm.BlueGain);
        Assert.Contains("Neutral", vm.ApproxColorProfileText);
    }

    [Fact]
    public void AudioService_DiscoversEndpoints_AndIdentifiesActiveDevice()
    {
        var audioService = new CoreAudioDeviceService();
        var devices = audioService.GetConnectedDevices();

        Assert.NotEmpty(devices);
        Assert.NotNull(audioService.ActiveDevice);
        Assert.InRange(audioService.ActiveDevice.VolumePercent, 0, 100);
    }

    [Fact]
    public void EqualizerApoBridge_DetectsInstallationStatus_Transparently()
    {
        var apoBridge = new EqualizerApoBridgeService();
        var status = apoBridge.CheckStatus();

        Assert.NotNull(status);
        Assert.NotEmpty(status.StatusMessage);
        Assert.NotEmpty(status.GuidanceTip);
    }

    [Fact]
    public void ProfileStorage_InitializesWithCalibratedProfilesAndEnvironments()
    {
        var storage = new ProfileStorageService();
        
        var displayProfiles = storage.GetDisplayProfiles();
        var audioProfiles = storage.GetAudioProfiles();
        var environments = storage.GetPresets();

        Assert.True(displayProfiles.Count >= 4);
        Assert.True(audioProfiles.Count >= 8);
        Assert.True(environments.Count >= 4);
        Assert.NotNull(storage.ActivePreset);
    }

    [Fact]
    public void MainViewModel_NavigatesCleanly_AcrossAllScreens()
    {
        var displayService = new DdcCiMonitorService();
        var audioService = new CoreAudioDeviceService();
        var apoBridge = new EqualizerApoBridgeService();
        var storage = new ProfileStorageService();

        var homeVm = new HomeViewModel(displayService, audioService, storage);
        var displayVm = new DisplayStudioViewModel(displayService, storage);
        var audioVm = new AudioStudioViewModel(audioService, apoBridge, storage);
        var envVm = new EnvironmentsViewModel(storage, displayService, audioService, apoBridge, storage);
        var settingsVm = new SettingsViewModel(displayService, audioService, apoBridge, storage);

        var mainVm = new MainViewModel(homeVm, displayVm, audioVm, envVm, settingsVm);

        Assert.Equal(homeVm, mainVm.CurrentView);
        Assert.Equal("Overview", mainVm.CurrentSectionTitle);

        mainVm.Navigate("Display");
        Assert.Equal(displayVm, mainVm.CurrentView);
        Assert.Equal("Display Studio", mainVm.CurrentSectionTitle);

        mainVm.Navigate("Audio");
        Assert.Equal(audioVm, mainVm.CurrentView);
        Assert.Equal("Audio Studio", mainVm.CurrentSectionTitle);

        mainVm.Navigate("Environments");
        Assert.Equal(envVm, mainVm.CurrentView);
        Assert.Equal("Environments", mainVm.CurrentSectionTitle);

        mainVm.Navigate("Settings");
        Assert.Equal(settingsVm, mainVm.CurrentView);
        Assert.Equal("Settings & Diagnostics", mainVm.CurrentSectionTitle);

        mainVm.Navigate("Home");
        Assert.Equal(homeVm, mainVm.CurrentView);
    }

    [Fact]
    public void AudioStudioViewModel_ResetsEqToFlat()
    {
        var audioService = new CoreAudioDeviceService();
        var apoBridge = new EqualizerApoBridgeService();
        var storage = new ProfileStorageService();

        var audioVm = new AudioStudioViewModel(audioService, apoBridge, storage);
        
        audioVm.Gain32Hz = 5.0;
        audioVm.Gain1kHz = -3.0;
        audioVm.ResetEq();

        Assert.Equal(0.0, audioVm.Gain32Hz);
        Assert.Equal(0.0, audioVm.Gain1kHz);
    }

    [Fact]
    public void EqualizerApoBridge_GeneratesValidConfigSyntax_WithAutoPreampHeadroom()
    {
        var bridge = new EqualizerApoBridgeService();
        var profile = new AudioProfile
        {
            Name = "Bass Heavy Master",
            BandGains = new double[] { 6.0, 4.0, 2.0, 0, 0, 0, 0, 0, 0, 0 },
            BassGainDb = 2.5,
            TrebleGainDb = 1.0,
            PreampDb = 0.0,
            IsLoudnessNormalizationEnabled = true,
            IsStereoWideningEnabled = true,
            StereoWidthPercent = 125
        };

        profile.ParametricFilters.Add(new ParametricFilter
        {
            Frequency = 2400,
            GainDb = 3.5,
            Q = 2.0,
            Type = ParametricFilterType.Peaking
        });

        string config = bridge.GenerateConfigString(profile, "Cosmic Byte Proteus");

        Assert.Contains("Device: \"Cosmic Byte Proteus\"", config);
        Assert.Contains("Preamp: -6.0 dB", config);
        Assert.Contains("GraphicEQ: 32 6.0; 64 4.0; 125 2.0; 250 0.0; 500 0.0; 1000 0.0; 2000 0.0; 4000 0.0; 8000 0.0; 16000 0.0", config);
        Assert.Contains("Filter: ON LSC Fc 100 Hz Gain 2.5 dB", config);
        Assert.Contains("Filter: ON HSC Fc 10000 Hz Gain 1.0 dB", config);
        Assert.Contains("Filter: ON PK Fc 2400 Hz Gain 3.5 dB Q 2.00", config);
        Assert.Contains("Copy: L=1.125*L+-0.125*R R=-0.125*L+1.125*R", config);

        var validation = bridge.ValidateConfigSyntax(config);
        Assert.True(validation.IsSuccess);
    }

    [Fact]
    public void EqualizerApoBridge_BypassMode_EmitsPureUnityPassthrough()
    {
        var bridge = new EqualizerApoBridgeService();
        var profile = new AudioProfile
        {
            Name = "Cinema Tuning",
            BandGains = new double[] { 5.0, 4.0, 3.0, 0, 0, 0, 0, 0, 0, 0 },
            IsBypassed = true
        };

        string config = bridge.GenerateConfigString(profile);

        Assert.Contains("BYPASS MODE ACTIVE", config);
        Assert.Contains("Preamp: 0.0 dB", config);
        Assert.DoesNotContain("GraphicEQ:", config);

        var validation = bridge.ValidateConfigSyntax(config);
        Assert.True(validation.IsSuccess);
    }

    [Fact]
    public void EqualizerApoBridge_ValidatesConfigSyntax_RejectsMalformedDirectives()
    {
        var bridge = new EqualizerApoBridgeService();

        string invalidConfig = @"
# Valid comment
Preamp: 0.0 dB
InvalidDirective: SomeRandomStuff
GraphicEQ: 32 0.0; 64 0.0
";

        var validation = bridge.ValidateConfigSyntax(invalidConfig);
        Assert.False(validation.IsSuccess);
        Assert.Contains("InvalidDirective", validation.Error);
    }

    [Fact]
    public void AudioResponseCalculator_EvaluatesGainAccurately_AndGeneratesSmoothPoints()
    {
        var profile = new AudioProfile
        {
            BandGains = new double[] { 0, 6.0, 0, 0, 0, 0, 0, 0, 0, 0 },
            BassGainDb = 0.0,
            TrebleGainDb = 0.0,
            PreampDb = 0.0,
            IsLoudnessNormalizationEnabled = false
        };

        double gainAt64 = AudioResponseCalculator.EvaluateGainDb(64.0, profile);
        Assert.InRange(gainAt64, 5.8, 6.2);

        double gainAt10k = AudioResponseCalculator.EvaluateGainDb(10000.0, profile);
        Assert.InRange(gainAt10k, -0.2, 0.2);

        var points = AudioResponseCalculator.GenerateResponseCurvePoints(profile, 800, 140, 160);
        Assert.Equal(160, points.Count);

        foreach (var pt in points)
        {
            Assert.InRange(pt.X, 0, 800);
            Assert.InRange(pt.Y, 0, 140);
        }
    }

    [Fact]
    public async Task AudioProfiles_CreateDuplicateRenameDelete_WorksAndPersists()
    {
        var storage = new ProfileStorageService();

        var gains = new double[] { 4.0, 3.0, 2.0, 1.0, 0, 0, 1.0, 2.0, 3.0, 4.0 };
        var createResult = await storage.CreateCustomAudioProfileAsync("Acoustic Signature", gains, 2.0, 1.5);
        Assert.True(createResult.IsSuccess);
        var created = createResult.Value!;
        Assert.Equal("Acoustic Signature", created.Name);
        Assert.True(created.IsCustom);
        Assert.Equal(4.0, created.BandGains[0]);

        var dupResult = await storage.DuplicateAudioProfileAsync(created.Id);
        Assert.True(dupResult.IsSuccess);
        var duplicated = dupResult.Value!;
        Assert.Contains("Copy", duplicated.Name);
        Assert.Equal(2.0, duplicated.BassGainDb);

        var renameResult = await storage.RenameAudioProfileAsync(duplicated.Id, "Acoustic Signature MkII");
        Assert.True(renameResult.IsSuccess);
        var renamed = storage.GetAudioProfiles().FirstOrDefault(p => p.Id == duplicated.Id);
        Assert.Equal("Acoustic Signature MkII", renamed?.Name);

        var deleteResult = await storage.DeleteAudioProfileAsync(created.Id);
        Assert.True(deleteResult.IsSuccess);
        Assert.DoesNotContain(storage.GetAudioProfiles(), p => p.Id == created.Id);

        await storage.DeleteAudioProfileAsync(duplicated.Id);
    }

    [Fact]
    public void WasapiVisualizer_Has32Bands_AndHannWindowInitialized()
    {
        using var visualizer = new WasapiLoopbackVisualizer();
        Assert.Equal(32, visualizer.BandCount);

        var bands = visualizer.GetCurrentBands();
        Assert.NotNull(bands);
        Assert.Equal(32, bands.Length);

        foreach (var b in bands)
        {
            Assert.InRange(b, 0.0f, 1.0f);
        }
    }

    // =========================================================================
    // PHASE 1D SPECIFIC TESTS: UNIFIED ENVIRONMENTS & WINDOWS INTEGRATION
    // =========================================================================

    [Fact]
    public async Task EnvironmentEngine_AppliesEnvironment_AndHandlesPartialStateTransparently()
    {
        var storage = new ProfileStorageService();
        var displayService = new DdcCiMonitorService();
        var audioService = new CoreAudioDeviceService();
        var apoBridge = new EqualizerApoBridgeService();

        var lateNight = storage.GetPresets().First(p => p.Id == "env_night");

        // Apply environment coordinating display and audio
        var appResult = await storage.ApplyEnvironmentAsync(lateNight.Id, displayService, audioService, apoBridge);

        Assert.True(appResult.IsSuccess);
        var res = appResult.Value!;
        Assert.Equal("Late Night", res.EnvironmentName);

        // Display hardware succeeds (Acer ED270 X0 active)
        Assert.True(res.DisplaySucceeded);

        // Coordinate audio subsystem integration depending on whether Equalizer APO is present on the host
        if (apoBridge.CheckStatus().IsInstalled)
        {
            Assert.True(res.AudioSucceeded);
            Assert.False(res.IsPartial);
        }
        else
        {
            Assert.False(res.AudioSucceeded);
            Assert.True(res.IsPartial);
            Assert.Contains("Partial Environment", res.FeedbackMessage);
            Assert.Contains("Equalizer APO is not installed", res.AudioMessage);
        }

        // Active environment is tracked
        Assert.Equal("Late Night", storage.ActivePreset?.Name);
    }

    [Fact]
    public async Task EnvironmentEngine_CRUD_CreateDuplicateRenameDelete_Persists()
    {
        var storage = new ProfileStorageService();
        var disp = storage.GetDisplayProfiles().First();
        var aud = storage.GetAudioProfiles().First();

        // 1. Create Custom Environment
        var createRes = await storage.CreateCustomEnvironmentAsync(
            "Midnight Studio 240Hz",
            "Specialized late-night coding with high refresh rate",
            "MoonIcon",
            disp,
            aud);

        Assert.True(createRes.IsSuccess);
        var created = createRes.Value!;
        Assert.Equal("Midnight Studio 240Hz", created.Name);
        Assert.True(created.IsCustom);

        // 2. Duplicate Environment
        var dupRes = await storage.DuplicateEnvironmentAsync(created.Id);
        Assert.True(dupRes.IsSuccess);
        var duplicated = dupRes.Value!;
        Assert.Contains("Copy", duplicated.Name);
        Assert.True(duplicated.IsCustom);

        // 3. Rename Environment
        var renRes = await storage.RenameEnvironmentAsync(duplicated.Id, "Midnight Studio MkII");
        Assert.True(renRes.IsSuccess);
        var renamed = storage.GetPresets().FirstOrDefault(e => e.Id == duplicated.Id);
        Assert.Equal("Midnight Studio MkII", renamed?.Name);

        // 4. Delete Custom Environment
        var delRes = await storage.DeleteEnvironmentAsync(created.Id);
        Assert.True(delRes.IsSuccess);
        Assert.DoesNotContain(storage.GetPresets(), e => e.Id == created.Id);

        // Clean up
        await storage.DeleteEnvironmentAsync(duplicated.Id);
    }

    [Fact]
    public void GlobalHotkeyService_DefinesWinAltP_AndHandlesLifecycle()
    {
        using var hotkeyService = new GlobalHotkeyService();
        Assert.Equal("Win + Alt + P", hotkeyService.ShortcutText);
        Assert.False(hotkeyService.IsRegistered);

        // Unregister on clean instance does not throw
        hotkeyService.Unregister();
        Assert.False(hotkeyService.IsRegistered);
    }

    [Fact]
    public async Task SettingsPersistence_SavesAndRestores_Preferences()
    {
        var storage = new ProfileStorageService();
        var settings = storage.GetSettings();

        bool originalMinimize = settings.MinimizeToTray;
        settings.MinimizeToTray = !originalMinimize;
        await storage.SaveSettingsAsync(settings);

        var reloaded = storage.GetSettings();
        Assert.Equal(!originalMinimize, reloaded.MinimizeToTray);

        // Restore original
        settings.MinimizeToTray = originalMinimize;
        await storage.SaveSettingsAsync(settings);
    }

    [Fact]
    public void QuickControlFlyoutViewModel_SyncsState_AndDispatchesCommands()
    {
        var displayService = new DdcCiMonitorService();
        var audioService = new CoreAudioDeviceService();
        var apoBridge = new EqualizerApoBridgeService();
        var storage = new ProfileStorageService();

        string navigated = "";
        var vm = new QuickControlFlyoutViewModel(
            displayService,
            audioService,
            storage,
            apoBridge,
            dest => navigated = dest);

        Assert.NotEmpty(vm.ActiveMonitorName);
        Assert.NotEmpty(vm.ActiveDeviceName);
        Assert.InRange(vm.Brightness, 0, 100);
        Assert.InRange(vm.MasterVolume, 0, 100);
        Assert.True(vm.Presets.Count >= 4);

        // Test navigation routing
        vm.OpenDisplayStudio();
        Assert.Equal("Display", navigated);

        vm.OpenAudioStudio();
        Assert.Equal("Audio", navigated);

        // Test Close Request
        bool closeRequested = false;
        vm.RequestClose += () => closeRequested = true;
        vm.CloseFlyout();
        Assert.True(closeRequested);
    }

    [Fact]
    public void WindowsStartupService_ChecksAndConfiguresRegistry_Safely()
    {
        // Safe check without modifying state
        bool isEnabled = WindowsStartupService.IsStartupEnabled();
        Assert.False(isEnabled); // By default disabled as required
    }

    [Fact]
    public async Task Hardware_AcerED270X0_ComprehensiveAudit()
    {
        var svc = new DdcCiMonitorService();
        var monitors = svc.GetConnectedMonitors();
        Assert.NotEmpty(monitors);

        var mon = monitors[0];
        Assert.Contains("ED270", mon.Name);
        int initialBr = mon.CurrentBrightness;
        int initialCt = mon.CurrentContrast;

        // 1. Luminance write & readback
        var writeRes = await svc.SetBrightnessAsync(mon.Id, 50);
        Assert.True(writeRes.IsSuccess);
        await Task.Delay(500); // Allow debouncer (50ms) + I2C wire write and scaler settling
        var readback = await svc.RefreshHardwareBrightnessAsync(mon.Id);
        Assert.True(readback.IsSuccess);
        Assert.Equal(50, readback.Value);

        // Restore Luminance
        await svc.SetBrightnessAsync(mon.Id, initialBr);
        await Task.Delay(500);
        var readbackRestore = await svc.RefreshHardwareBrightnessAsync(mon.Id);
        Assert.True(readbackRestore.IsSuccess);
        Assert.Equal(initialBr, readbackRestore.Value);

        // 2. Contrast write & readback
        await svc.SetContrastAsync(mon.Id, 52);
        await Task.Delay(350);
        await svc.RefreshAllHardwareValuesAsync(mon.Id);
        mon = svc.ActiveMonitor ?? mon;
        Assert.Equal(52, mon.CurrentContrast);

        // Restore Contrast
        await svc.SetContrastAsync(mon.Id, initialCt);
        await Task.Delay(500);
        await svc.RefreshAllHardwareValuesAsync(mon.Id);
        mon = svc.ActiveMonitor ?? mon;
        Assert.Equal(initialCt, mon.CurrentContrast);

        // 3. RGB Gains write & readback
        await svc.SetRgbGainsAsync(mon.Id, 54, 48, 42);
        await Task.Delay(600);
        await svc.RefreshAllHardwareValuesAsync(mon.Id);
        mon = svc.ActiveMonitor ?? mon;
        Assert.Equal(54, mon.RedGain);
        Assert.Equal(48, mon.GreenGain);
        Assert.Equal(42, mon.BlueGain);

        // Restore Neutral RGB
        await svc.SetRgbGainsAsync(mon.Id, 50, 50, 50);
        await Task.Delay(600);
        await svc.RefreshAllHardwareValuesAsync(mon.Id);
        mon = svc.ActiveMonitor ?? mon;
        Assert.Equal(50, mon.RedGain);
        Assert.Equal(50, mon.GreenGain);
        Assert.Equal(50, mon.BlueGain);

        // 4. Sub-Zero Software Dimmer
        using var dimmer = new SoftwareDimmerService();
        dimmer.SetDimming(0, 0, 1920, 1080, true, 20);
        dimmer.ClearAll();
    }
}
