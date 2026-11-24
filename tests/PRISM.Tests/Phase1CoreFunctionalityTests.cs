using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PRISM.Core.Common;
using PRISM.Core.Interfaces;
using PRISM.Core.Models;
using PRISM.Hardware.Audio;
using PRISM.Hardware.Diagnostics;
using PRISM.Hardware.Display;
using PRISM.Hardware.Storage;
using Xunit;

namespace PRISM.Tests;

public class Phase1CoreFunctionalityTests : IDisposable
{
    private readonly string _tempTestDir;

    public Phase1CoreFunctionalityTests()
    {
        _tempTestDir = Path.Combine(Path.GetTempPath(), "PRISM_Phase1Tests_" + Guid.NewGuid().ToString("N"));
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

    // =========================================================================
    // SECTION 3: WINDOWS NATIVE BRIGHTNESS INTEGRATION TESTS
    // =========================================================================

    [Fact]
    public void WindowsBrightnessService_ProbesTopology_HonestReportingForExternalMonitor()
    {
        var service = new WindowsBrightnessIntegrationService();
        var cap = service.QueryCapabilities("ACR0F95_154604D5A3W01");

        Assert.NotNull(cap);

        // On this desktop workstation with Acer ED270 X0 over DisplayPort,
        // no internal ACPI laptop backlight exists.
        Assert.False(cap.IsInternalDisplay);
        Assert.False(cap.IsQuickSettingsSyncSupported);
        Assert.Equal(WindowsBrightnessSupportType.UnsupportedExternalDdcCi, cap.SupportType);
        Assert.Contains("DDC/CI", cap.RecommendedInterface);
        Assert.Contains("WmiMonitorBrightness", cap.TechnicalLimitationExplanation);
        Assert.Contains("external desktop", cap.TechnicalLimitationExplanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WindowsBrightnessService_SetBrightness_FailsSafelyWhenUnsupported()
    {
        var service = new WindowsBrightnessIntegrationService();
        var cap = service.QueryCapabilities();

        if (!cap.IsQuickSettingsSyncSupported)
        {
            var res = await service.SetWindowsBrightnessAsync(65);
            Assert.False(res.IsSuccess);
            Assert.Contains("WmiMonitorBrightness", res.Error);
        }
    }

    // =========================================================================
    // SECTION 4: AUDIO PIPELINE & EQUALIZER APO HOOK DETECTION TESTS
    // =========================================================================

    [Fact]
    public void EqualizerApoBridge_EndpointHookDetection_ReportsGenuineStatus()
    {
        var apoBridge = new EqualizerApoBridgeService();
        var status = apoBridge.CheckStatus("cosmic byte proteus god mode");

        Assert.NotNull(status);
        Assert.True(status.IsInstalled, "Equalizer APO 1.4.2 is installed in Program Files");

        // Real check: inspects MMDevices registry FxProperties
        bool isHooked = apoBridge.IsDeviceHookedByEqualizerApo("cosmic byte proteus god mode");
        Assert.Equal(isHooked, status.IsConfiguredForActiveDevice);

        if (!isHooked)
        {
            Assert.Contains("not enabled in Configurator", status.StatusMessage);
            Assert.Contains("Configurator.exe", status.GuidanceTip);
        }
    }

    // =========================================================================
    // SECTION 7: PRISM GENUINE HEALTH CHECK TESTS
    // =========================================================================

    [Fact]
    public async Task HealthCheckService_RunsAll13SubsystemProbes_ReturnsValidReport()
    {
        var storage = new ProfileStorageService(_tempTestDir);
        var displayService = new DdcCiMonitorService();
        var audioService = new CoreAudioDeviceService();
        var apoBridge = new EqualizerApoBridgeService();
        var winBrightness = new WindowsBrightnessIntegrationService();

        var healthService = new HealthCheckService(
            displayService, audioService, apoBridge, winBrightness, storage, storage);

        var report = await healthService.RunDiagnosticsAsync();

        Assert.NotNull(report);
        Assert.Equal(13, report.Items.Count);

        // Verify the 13 required component probes
        string[] requiredComponents = new[]
        {
            "Display Connection",
            "DDC/CI Communication",
            "Hardware Luminance (VCP 0x10)",
            "Hardware Contrast (VCP 0x12)",
            "RGB Hardware Video Gains",
            "Windows Brightness Integration",
            "Active Audio Endpoint",
            "Audio Volume & Channel Attenuation",
            "Equalizer APO Integration",
            "Audio DSP Processing",
            "Global Shortcut (Win+Alt+P)",
            "Profile Storage & Persistence",
            "Windows Startup & System Tray"
        };

        foreach (var req in requiredComponents)
        {
            var item = report.Items.FirstOrDefault(i => i.Component == req);
            Assert.NotNull(item);
            Assert.NotEqual(HealthStatus.NotTested, item.Status);
            Assert.False(string.IsNullOrWhiteSpace(item.Details));
            Assert.False(string.IsNullOrWhiteSpace(item.TechnicalMetrics));
            Assert.False(string.IsNullOrWhiteSpace(item.StatusText));
            Assert.False(string.IsNullOrWhiteSpace(item.StatusBadgeBackground));
            Assert.False(string.IsNullOrWhiteSpace(item.StatusBadgeForeground));
        }

        Assert.Contains("PRISM STUDIO GENUINE HEALTH CHECK", report.FormattedSummaryText);
        Assert.Contains("Windows 11 x64", report.FormattedSummaryText);
    }

    [Fact]
    public void HealthCheckItem_BadgeProperties_ProduceAccessibleColors()
    {
        var healthy = new HealthCheckItem { Status = HealthStatus.Healthy };
        Assert.Equal("HEALTHY", healthy.StatusText);
        Assert.Contains("63C99A", healthy.StatusBadgeForeground);

        var limited = new HealthCheckItem { Status = HealthStatus.Limited };
        Assert.Equal("LIMITED", limited.StatusText);
        Assert.Contains("D6A76A", limited.StatusBadgeForeground);

        var unavail = new HealthCheckItem { Status = HealthStatus.Unavailable };
        Assert.Equal("UNAVAILABLE", unavail.StatusText);
        Assert.Contains("8993A0", unavail.StatusBadgeForeground);

        var error = new HealthCheckItem { Status = HealthStatus.Error };
        Assert.Equal("ERROR", error.StatusText);
        Assert.Contains("E17D85", error.StatusBadgeForeground);
    }
}
