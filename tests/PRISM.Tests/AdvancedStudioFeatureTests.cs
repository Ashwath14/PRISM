using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PRISM.Core.Models;
using PRISM.Hardware.Audio;
using PRISM.Hardware.Display;
using PRISM.Hardware.Storage;
using Xunit;

namespace PRISM.Tests;

public class AdvancedStudioFeatureTests : IDisposable
{
    private readonly string _tempTestDir;

    public AdvancedStudioFeatureTests()
    {
        _tempTestDir = Path.Combine(Path.GetTempPath(), "PRISM_Advanced_Tests_" + Guid.NewGuid().ToString("N"));
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
    public void DisplayResolutionService_EnumeratesSupportedModes_FindsNativeResolution()
    {
        using var resService = new DisplayResolutionService();
        var modes = resService.GetSupportedModes();

        Assert.NotNull(modes);
        Assert.NotEmpty(modes);

        // Native resolution must be identified
        var nativeMode = modes.FirstOrDefault(m => m.IsNative);
        Assert.NotNull(nativeMode);
        Assert.True(nativeMode.Width >= 1280);
        Assert.True(nativeMode.Height >= 720);
        Assert.True(nativeMode.RefreshRate >= 60);

        // Current mode must be active
        var currentMode = resService.GetCurrentMode();
        Assert.NotNull(currentMode);
        Assert.True(currentMode.Width > 0);
        Assert.True(currentMode.Height > 0);
        Assert.True(currentMode.RefreshRate > 0);
        Assert.True(currentMode.IsActive);
    }

    [Fact]
    public void DisplayAdvancedColorService_QueriesPanel_ReturnsHonestSdrReport()
    {
        var colorService = new DisplayAdvancedColorService();
        var info = colorService.GetAdvancedColorInfo();

        Assert.NotNull(info);
        Assert.NotNull(info.ColorEncoding);
        Assert.NotNull(info.DolbyVisionStatus);
        Assert.NotNull(info.SummaryText);
        Assert.True(info.BitsPerChannel >= 8);

        // Verify Dolby Vision honesty: must explain hardware requirement, not fake support
        Assert.Contains("Dolby Vision", info.DolbyVisionStatus, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SpatialAudioService_QueriesSubsystem_ReturnsStatusAndFormat()
    {
        var spatialService = new SpatialAudioService();
        var info = spatialService.GetSpatialAudioInfo();

        Assert.NotNull(info);
        Assert.NotNull(info.ActiveFormatName);
        Assert.NotNull(info.StatusMessage);
        Assert.False(string.IsNullOrWhiteSpace(info.ActiveFormatName));
    }

    [Fact]
    public void ProfileStorageService_DisplayProfiles_Contains4StandardFactoryProfiles()
    {
        var storage = new ProfileStorageService(_tempTestDir);
        var profiles = storage.GetDisplayProfiles();

        Assert.NotNull(profiles);
        Assert.Equal(4, profiles.Count);

        var names = profiles.Select(p => p.Name).ToList();
        Assert.Contains("Daylight Focus", names);
        Assert.Contains("Late Night", names);
        Assert.Contains("Competitive Gaming", names);
        Assert.Contains("Cinematic Movie", names);

        // Verify values
        var daylight = profiles.First(p => p.Name == "Daylight Focus");
        Assert.Equal(55, daylight.Brightness);
        Assert.Equal(50, daylight.Contrast);
        Assert.Equal(50, daylight.RedGain);
        Assert.True(daylight.IsBuiltIn);

        var night = profiles.First(p => p.Name == "Late Night");
        Assert.Equal(25, night.Brightness);
        Assert.True(night.RedGain > night.BlueGain); // Warm tint
        Assert.True(night.IsBuiltIn);

        var gaming = profiles.First(p => p.Name == "Competitive Gaming");
        Assert.Equal(75, gaming.Brightness);
        Assert.True(gaming.Brightness > 50, "Competitive Gaming must never reduce brightness to 0% or low levels");
        Assert.True(gaming.IsBuiltIn);

        var cinema = profiles.First(p => p.Name == "Cinematic Movie");
        Assert.Equal(60, cinema.Brightness);
        Assert.Equal(55, cinema.Contrast);
        Assert.True(cinema.IsBuiltIn);
    }

    [Fact]
    public void ProfileStorageService_AudioProfiles_Contains16CalibratedPresets()
    {
        var storage = new ProfileStorageService(_tempTestDir);
        var presets = storage.GetAudioProfiles();

        Assert.NotNull(presets);
        Assert.Equal(16, presets.Count);

        var names = presets.Select(p => p.Name).ToList();
        Assert.Contains("Flat / Reference", names);
        Assert.Contains("Deep Bass", names);
        Assert.Contains("Punchy Bass", names);
        Assert.Contains("Clear Bass", names);
        Assert.Contains("Vocal Clarity", names);
        Assert.Contains("Balanced Music", names);
        Assert.Contains("Acoustic", names);
        Assert.Contains("Classical", names);
        Assert.Contains("Rock", names);
        Assert.Contains("Pop", names);
        Assert.Contains("Electronic", names);
        Assert.Contains("Movie / Cinema", names);
        Assert.Contains("Gaming / Footsteps", names);
        Assert.Contains("Voice / Podcast", names);
        Assert.Contains("Night Listening", names);
        Assert.Contains("Loudness Balanced", names);

        // Validate each preset has 10 bands
        foreach (var p in presets)
        {
            Assert.NotNull(p.BandGains);
            Assert.Equal(10, p.BandGains.Length);
        }
    }

    [Fact]
    public void ProfileStorageService_HeadphoneProfiles_ContainsAutoEqReferenceModels()
    {
        var storage = new ProfileStorageService(_tempTestDir);
        var hps = storage.GetHeadphoneProfiles();

        Assert.NotNull(hps);
        Assert.True(hps.Count >= 6);

        var models = hps.Select(h => h.Model).ToList();
        Assert.Contains(models, m => m.Contains("HD 600"));
        Assert.Contains(models, m => m.Contains("ATH-M50x"));
        Assert.Contains(models, m => m.Contains("WH-1000XM4"));
        Assert.Contains(models, m => m.Contains("AirPods Max"));
        Assert.Contains(models, m => m.Contains("DT 770"));
        Assert.Contains(models, m => m.Contains("Harman In-Ear"));

        // Verify preamp attenuation to prevent digital clipping
        foreach (var hp in hps)
        {
            Assert.True(hp.PreampDb <= 0.0);
            Assert.NotEmpty(hp.ParametricFilters);
        }
    }

    [Fact]
    public async Task ProfileStorageService_ImportAutoEqProfile_ParsesStandardAutoEqFormat()
    {
        var storage = new ProfileStorageService(_tempTestDir);

        string testAutoEqContent = 
@"# AutoEq ParametricEQ export
Preamp: -4.8 dB
Filter 1: ON PK Fc 32 Hz Gain 5.2 dB Q 1.10
Filter 2: ON LSC Fc 105 Hz Gain 3.8 dB Q 0.71
Filter 3: ON PK Fc 220 Hz Gain -2.4 dB Q 2.10
Filter 4: ON HSC Fc 10000 Hz Gain -3.0 dB Q 0.70
";

        string filePath = Path.Combine(_tempTestDir, "Sennheiser_HD560S_ParametricEQ.txt");
        File.WriteAllText(filePath, testAutoEqContent);

        var res = await storage.ImportAutoEqProfileAsync(filePath);
        Assert.True(res.IsSuccess);
        Assert.NotNull(res.Value);

        var hp = res.Value;
        Assert.Equal(-4.8, hp.PreampDb);
        Assert.Equal(4, hp.ParametricFilters.Count);

        // Check filter 1
        var f1 = hp.ParametricFilters[0];
        Assert.Equal(ParametricFilterType.Peaking, f1.Type);
        Assert.Equal(32, f1.Frequency);
        Assert.Equal(5.2, f1.GainDb);
        Assert.Equal(1.10, f1.Q);

        // Check filter 2 (Low Shelf)
        var f2 = hp.ParametricFilters[1];
        Assert.Equal(ParametricFilterType.LowShelf, f2.Type);
        Assert.Equal(105, f2.Frequency);
        Assert.Equal(3.8, f2.GainDb);

        // Check filter 4 (High Shelf)
        var f4 = hp.ParametricFilters[3];
        Assert.Equal(ParametricFilterType.HighShelf, f4.Type);
        Assert.Equal(10000, f4.Frequency);
        Assert.Equal(-3.0, f4.GainDb);
    }

    [Fact]
    public async Task ChannelBalance_AudioService_GetsAndSetsBalance()
    {
        var audioService = new CoreAudioDeviceService();
        var getRes = await audioService.GetChannelBalanceAsync();

        Assert.True(getRes.IsSuccess);
        Assert.True(getRes.Value.Left >= 0.0f && getRes.Value.Left <= 1.0f);
        Assert.True(getRes.Value.Right >= 0.0f && getRes.Value.Right <= 1.0f);

        // Set to center
        var setRes = await audioService.SetChannelBalanceAsync(1.0f, 1.0f);
        Assert.True(setRes.IsSuccess);
    }

    [Fact]
    public void AudioResponseCalculator_BiquadTransfer_CalculatesResponsePoints()
    {
        var profile = new AudioProfile
        {
            BandGains = new double[] { 6.0, 5.0, 3.0, 0.0, 0.0, 0.0, 1.0, 2.0, 4.0, 5.0 },
            BassGainDb = 3.0,
            TrebleGainDb = 2.0,
            PreampDb = -4.0
        };

        var points = AudioResponseCalculator.GenerateResponseCurvePoints(profile, 780, 130, 160);
        Assert.NotNull(points);
        Assert.Equal(160, points.Count);

        // Verify valid coordinates within bounding box
        foreach (var pt in points)
        {
            Assert.True(pt.X >= 0 && pt.X <= 780);
            Assert.True(pt.Y >= 0 && pt.Y <= 130);
        }
    }
}
