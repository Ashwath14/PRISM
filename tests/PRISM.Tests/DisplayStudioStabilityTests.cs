using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using PRISM.App.ViewModels;
using PRISM.Hardware.Audio;
using PRISM.Hardware.Display;
using PRISM.Hardware.Storage;
using Xunit;

namespace PRISM.Tests;

public class DisplayStudioStabilityTests : IDisposable
{
    private readonly string _tempTestDir;

    public DisplayStudioStabilityTests()
    {
        _tempTestDir = Path.Combine(Path.GetTempPath(), "PRISM_Stability_Tests_" + Guid.NewGuid().ToString("N"));
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
    public void PrismIcons_ContainsWarningIconGeometry()
    {
        string solutionDir = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(solutionDir) && !File.Exists(Path.Combine(solutionDir, "PRISM.sln")))
        {
            var parent = Directory.GetParent(solutionDir);
            if (parent == null) break;
            solutionDir = parent.FullName;
        }

        string iconsPath = Path.Combine(solutionDir, "src", "PRISM.App", "Styles", "PrismIcons.xaml");
        Assert.True(File.Exists(iconsPath), $"Cannot find PrismIcons.xaml at {iconsPath}");

        string content = File.ReadAllText(iconsPath);
        Assert.Contains("x:Key=\"WarningIconGeometry\"", content);
    }

    [Fact]
    public void AllStaticResourceKeysInAllXamlFiles_AreDefinedInResourceDictionaries()
    {
        string solutionDir = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(solutionDir) && !File.Exists(Path.Combine(solutionDir, "PRISM.sln")))
        {
            var parent = Directory.GetParent(solutionDir);
            if (parent == null) break;
            solutionDir = parent.FullName;
        }

        string appDir = Path.Combine(solutionDir, "src", "PRISM.App");
        var xamlFiles = Directory.GetFiles(appDir, "*.xaml", SearchOption.AllDirectories);
        Assert.NotEmpty(xamlFiles);

        // Collect all defined keys
        var definedKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in xamlFiles)
        {
            string content = File.ReadAllText(file);
            var matches = Regex.Matches(content, @"x:Key=""([^""]+)""");
            foreach (Match m in matches)
            {
                definedKeys.Add(m.Groups[1].Value);
            }
        }

        Assert.True(definedKeys.Count >= 90, $"Expected >= 90 keys defined, found {definedKeys.Count}");
        Assert.Contains("WarningIconGeometry", definedKeys);
        Assert.Contains("VolumeIconGeometry", definedKeys);
        Assert.Contains("DisplayIconGeometry", definedKeys);
        Assert.Contains("PrismCardStyle", definedKeys);

        // Verify all StaticResource references resolve
        var missingKeys = new List<(string File, string Key)>();
        foreach (var file in xamlFiles)
        {
            string content = File.ReadAllText(file);
            var matches = Regex.Matches(content, @"StaticResource\s+([^,\}\s]+)");
            foreach (Match m in matches)
            {
                string key = m.Groups[1].Value;
                // Exclude local BooleanToVisibilityConverter keys defined in element scope
                if (key != "BoolToVis" && key != "BoolToVisConverter" && !definedKeys.Contains(key))
                {
                    missingKeys.Add((Path.GetFileName(file), key));
                }
            }
        }

        Assert.Empty(missingKeys);
    }

    [Fact]
    public void RepeatedNavigation_DisplayToHomeAndBack_20Times_Succeeds()
    {
        var storage = new ProfileStorageService(_tempTestDir);
        var displaySvc = new DdcCiMonitorService(null, storage);
        var audioSvc = new CoreAudioDeviceService();
        var apoBridge = new EqualizerApoBridgeService();
        var resSvc = new DisplayResolutionService();
        var colorSvc = new DisplayAdvancedColorService();
        var spatialSvc = new SpatialAudioService();

        var homeVm = new HomeViewModel(displaySvc, audioSvc, storage);
        var displayVm = new DisplayStudioViewModel(displaySvc, storage, resSvc, colorSvc);
        var audioVm = new AudioStudioViewModel(audioSvc, apoBridge, storage, spatialSvc);
        var envVm = new EnvironmentsViewModel(storage, displaySvc, audioSvc, apoBridge, storage);
        var settingsVm = new SettingsViewModel(displaySvc, audioSvc, apoBridge, storage);

        var mainVm = new MainViewModel(homeVm, displayVm, audioVm, envVm, settingsVm);

        // Simulate rapid repeated navigation 20 times across all studios
        for (int i = 0; i < 20; i++)
        {
            mainVm.Navigate("Display");
            Assert.Equal(displayVm, mainVm.CurrentView);
            Assert.True(mainVm.IsDisplaySelected);
            Assert.Equal("Display Studio", mainVm.CurrentSectionTitle);

            mainVm.Navigate("Home");
            Assert.Equal(homeVm, mainVm.CurrentView);
            Assert.True(mainVm.IsHomeSelected);
            Assert.Equal("Overview", mainVm.CurrentSectionTitle);

            mainVm.Navigate("Audio");
            Assert.Equal(audioVm, mainVm.CurrentView);
            Assert.True(mainVm.IsAudioSelected);
            Assert.Equal("Audio Studio", mainVm.CurrentSectionTitle);

            mainVm.Navigate("Environments");
            Assert.Equal(envVm, mainVm.CurrentView);
            Assert.True(mainVm.IsEnvironmentsSelected);
            Assert.Equal("Environments", mainVm.CurrentSectionTitle);

            mainVm.Navigate("Settings");
            Assert.Equal(settingsVm, mainVm.CurrentView);
            Assert.True(mainVm.IsSettingsSelected);
            Assert.Equal("Settings & Diagnostics", mainVm.CurrentSectionTitle);
        }
    }
}
