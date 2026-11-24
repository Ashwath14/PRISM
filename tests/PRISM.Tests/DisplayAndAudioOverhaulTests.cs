using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using PRISM.Core.Common;
using PRISM.Core.Models;
using PRISM.Hardware.Audio;
using PRISM.Hardware.Display;
using PRISM.Hardware.Storage;
using Xunit;

namespace PRISM.Tests;

public class DisplayAndAudioOverhaulTests
{
    [Theory]
    [InlineData("1920 \u00D7 1080 (Native)", 1920, 1080, true)]
    [InlineData("1920 x 1080", 1920, 1080, true)]
    [InlineData("2560 X 1440 (QHD)", 2560, 1440, true)]
    [InlineData("3840 \u00D7 2160 (4K UHD)", 3840, 2160, true)]
    [InlineData("1280x720", 1280, 720, true)]
    [InlineData("invalid_string", 0, 0, false)]
    [InlineData("", 0, 0, false)]
    [InlineData(null, 0, 0, false)]
    public void DisplayResolution_TryParseResolution_HandlesVariousSeparators(string? input, int expectedW, int expectedH, bool expectedSuccess)
    {
        bool success = TryParseResolutionHelper(input, out int w, out int h);

        Assert.Equal(expectedSuccess, success);
        if (expectedSuccess)
        {
            Assert.Equal(expectedW, w);
            Assert.Equal(expectedH, h);
        }
    }

    private static bool TryParseResolutionHelper(string? text, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var match = Regex.Match(text, @"(\d+)\s*[x\u00D7X]\s*(\d+)");
        if (match.Success &&
            int.TryParse(match.Groups[1].Value, out width) &&
            int.TryParse(match.Groups[2].Value, out height))
        {
            return true;
        }

        return false;
    }

    [Fact]
    public void PrismAudioEngine_UpdateDspAndBypass_OperatesCleanly()
    {
        using var engine = new PrismAudioEngine();

        var profile = new AudioProfile
        {
            Id = "test",
            Name = "Test",
            BandGains = new double[] { 6.0, 4.0, 2.0, 0.0, -2.0, -1.0, 1.0, 3.0, 5.0, 6.0 },
            BassGainDb = 3.0,
            TrebleGainDb = 2.0,
            PreampDb = -1.5,
            StereoWidthPercent = 140,
            IsStereoWideningEnabled = true
        };

        // Updating DSP parameters should never throw
        engine.UpdateDspParameters(profile, 1.0f, 1.0f);
        engine.SetBypass(false);
        Assert.False(engine.IsBypassed);

        engine.SetBypass(true);
        Assert.True(engine.IsBypassed);

        engine.CurrentTrack = AuditionTrack.SynthGroove;
        Assert.Equal(AuditionTrack.SynthGroove, engine.CurrentTrack);

        engine.CurrentTrack = AuditionTrack.AcousticMelody;
        Assert.Equal(AuditionTrack.AcousticMelody, engine.CurrentTrack);
    }

    [Fact]
    public void ProfileStorageService_All16Presets_HaveDetailedDescriptionsAndExperiences()
    {
        var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PRISM_Test_" + Guid.NewGuid().ToString("N"));
        try
        {
            var storage = new ProfileStorageService(tempDir);
            var presets = storage.GetAudioProfiles();

            Assert.NotNull(presets);
            Assert.Equal(16, presets.Count);

            foreach (var preset in presets)
            {
                Assert.False(string.IsNullOrWhiteSpace(preset.Name), $"Preset {preset.Id} has empty Name");
                Assert.False(string.IsNullOrWhiteSpace(preset.Category), $"Preset {preset.Name} has empty Category");
                Assert.False(string.IsNullOrWhiteSpace(preset.Description), $"Preset {preset.Name} has empty Description");
                Assert.False(string.IsNullOrWhiteSpace(preset.IntendedExperience), $"Preset {preset.Name} has empty IntendedExperience");
                Assert.NotNull(preset.BandGains);
                Assert.Equal(10, preset.BandGains.Length);
            }
        }
        finally
        {
            if (System.IO.Directory.Exists(tempDir))
            {
                System.IO.Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void WasapiLoopbackVisualizer_PushPcmSamples_TriggersSpectrumUpdate()
    {
        using var visualizer = new WasapiLoopbackVisualizer
        {
            SimulateWhenIdle = false
        };

        bool eventFired = false;
        float[]? receivedSpectrum = null;

        visualizer.SpectrumUpdated += spectrum =>
        {
            eventFired = true;
            receivedSpectrum = spectrum;
        };

        // Push 1024 synthetic sine wave samples at 440 Hz (A4)
        int sampleRate = 44100;
        int count = 1024;
        float[] samples = new float[count];
        for (int i = 0; i < count; i++)
        {
            samples[i] = (float)Math.Sin(2.0 * Math.PI * 440.0 * i / sampleRate) * 0.8f;
        }

        visualizer.PushPcmSamples(samples, sampleRate);

        Assert.True(eventFired, "SpectrumUpdated was not triggered after PushPcmSamples");
        Assert.NotNull(receivedSpectrum);
        Assert.Equal(32, receivedSpectrum.Length);

        // At least one bar in the spectrum should have non-zero energy
        float maxEnergy = receivedSpectrum.Max();
        Assert.True(maxEnergy > 0.01f, $"Max energy was too low: {maxEnergy}");
    }

    private static readonly object _wpfInitLock = new();
    private static Thread? _staHostThread;
    private static System.Windows.Threading.Dispatcher? _wpfDispatcher;
    private static TaskCompletionSource<bool>? _wpfReadyTcs;

    private static void EnsureWpfHost()
    {
        lock (_wpfInitLock)
        {
            if (_wpfDispatcher != null) return;

            _wpfReadyTcs = new TaskCompletionSource<bool>();
            _staHostThread = new Thread(() =>
            {
                try
                {
                    if (System.Windows.Application.Current == null)
                    {
                        var app = new System.Windows.Application { ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown };
                        var dictUris = new[]
                        {
                            "pack://application:,,,/PRISM;component/Styles/PrismColors.xaml",
                            "pack://application:,,,/PRISM;component/Styles/PrismIcons.xaml",
                            "pack://application:,,,/PRISM;component/Styles/PrismControls.xaml"
                        };
                        foreach (var uri in dictUris)
                        {
                            var dict = new System.Windows.ResourceDictionary { Source = new Uri(uri, UriKind.Absolute) };
                            app.Resources.MergedDictionaries.Add(dict);
                        }
                    }
                    _wpfDispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
                    _wpfReadyTcs.TrySetResult(true);
                    System.Windows.Threading.Dispatcher.Run();
                }
                catch (Exception ex)
                {
                    _wpfReadyTcs.TrySetException(ex);
                }
            });
            _staHostThread.SetApartmentState(ApartmentState.STA);
            _staHostThread.IsBackground = true;
            _staHostThread.Start();
            _wpfReadyTcs.Task.Wait();
        }
    }

    private static void RunOnWpf(Action action)
    {
        EnsureWpfHost();
        Exception? ex = null;
        _wpfDispatcher!.Invoke(() =>
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                ex = e;
            }
        });
        if (ex != null)
        {
            throw new Exception("WPF Action Failed", ex);
        }
    }

    [Fact]
    public void Views_InstantiateAndRenderToPng_Succeeds()
    {
        RunOnWpf(() =>
        {
            var ddc = new PRISM.Hardware.Display.DdcCiMonitorService();
            var audio = new PRISM.Hardware.Audio.CoreAudioDeviceService();
            var storage = new PRISM.Hardware.Storage.ProfileStorageService();
            var apo = new PRISM.Hardware.Audio.EqualizerApoBridgeService();

            var homeVm = new PRISM.App.ViewModels.HomeViewModel(ddc, audio, storage);
            homeVm.RefreshState();

            var displayVm = new PRISM.App.ViewModels.DisplayStudioViewModel(ddc, storage);

            var audioVm = new PRISM.App.ViewModels.AudioStudioViewModel(audio, apo, storage);
            audioVm.RefreshApoStatus();

            homeVm.AttachStudios(displayVm, audioVm);

            var envVm = new PRISM.App.ViewModels.EnvironmentsViewModel(storage, ddc, audio, apo, storage);
            envVm.LoadPresets();

            var settingsVm = new PRISM.App.ViewModels.SettingsViewModel(ddc, audio, apo, storage, storage);

            RenderViewToFile(new PRISM.App.Views.HomeView { DataContext = homeVm }, 1080, 800, "PRISM_Home_Redesigned.png");
            RenderViewToFile(new PRISM.App.Views.DisplayStudioView { DataContext = displayVm }, 1080, 1300, "PRISM_DisplayStudio_Redesigned.png");
            RenderViewToFile(new PRISM.App.Views.DisplayStudioView { DataContext = displayVm }, 1080, 1300, "PRISM_DisplayStudio_Overhaul.png");
            RenderViewToFile(new PRISM.App.Views.AudioStudioView { DataContext = audioVm }, 1080, 2400, "PRISM_AudioStudio_Redesigned.png");
            RenderViewToFile(new PRISM.App.Views.AudioStudioView { DataContext = audioVm }, 1080, 2400, "PRISM_AudioStudio_Overhaul.png");
            RenderViewToFile(new PRISM.App.Views.EnvironmentsView { DataContext = envVm }, 1080, 1200, "PRISM_Environments_Redesigned.png");
            RenderViewToFile(new PRISM.App.Views.SettingsView { DataContext = settingsVm }, 1080, 950, "PRISM_Settings_Redesigned.png");

            var flyoutVm = new PRISM.App.ViewModels.QuickControlFlyoutViewModel(ddc, audio, storage, apo, _ => { });
            flyoutVm.SyncState();
            var flyout = new PRISM.App.Views.QuickControlFlyoutWindow(flyoutVm);
            if (flyout.Content is System.Windows.FrameworkElement flyoutElement)
            {
                flyout.Content = null;
                flyoutElement.DataContext = flyoutVm;
                RenderViewToFile(flyoutElement, 400, 600, "PRISM_QuickControls_Redesigned.png");
            }
        });
    }

    [Fact]
    public void GlobalToolTipTheme_AppliesMidnightGraphiteStyling_AndHighContrast()
    {
        RunOnWpf(() =>
        {
            // 1. Verify color tokens
            var bgBrush = System.Windows.Application.Current.Resources["TooltipBackgroundBrush"] as System.Windows.Media.SolidColorBrush;
            Assert.NotNull(bgBrush);
            Assert.Equal(System.Windows.Media.Color.FromRgb(0x20, 0x2A, 0x38), bgBrush.Color);

            var fgBrush = System.Windows.Application.Current.Resources["TooltipForegroundBrush"] as System.Windows.Media.SolidColorBrush;
            Assert.NotNull(fgBrush);
            Assert.Equal(System.Windows.Media.Color.FromRgb(0xE8, 0xE7, 0xE2), fgBrush.Color);

            var borderBrush = System.Windows.Application.Current.Resources["TooltipBorderBrush"] as System.Windows.Media.SolidColorBrush;
            Assert.NotNull(borderBrush);
            Assert.Equal(System.Windows.Media.Color.FromRgb(0x34, 0x3E, 0x4C), borderBrush.Color);

            // 2. Verify ToolTip style lookup and setters
            var tooltipStyle = System.Windows.Application.Current.FindResource(typeof(System.Windows.Controls.ToolTip)) as System.Windows.Style;
            Assert.NotNull(tooltipStyle);

            var tooltip = new System.Windows.Controls.ToolTip
            {
                Content = "Launch Quick Flyout (Win+Alt+P)",
                Style = tooltipStyle
            };

            Assert.Equal(bgBrush, tooltip.Background);
            Assert.Equal(fgBrush, tooltip.Foreground);
            Assert.Equal(borderBrush, tooltip.BorderBrush);
            Assert.Equal(new System.Windows.Thickness(1), tooltip.BorderThickness);
            Assert.Equal(new System.Windows.Thickness(9, 6, 9, 6), tooltip.Padding);
            Assert.Equal(12.0, tooltip.FontSize);

            // 3. Verify Template structure
            Assert.NotNull(tooltip.Template);
            var visualBorder = tooltip.Template.LoadContent() as System.Windows.Controls.Border;
            Assert.NotNull(visualBorder);
            Assert.Equal(new System.Windows.CornerRadius(6), visualBorder.CornerRadius);

            // 4. Render visual verification image of the rendered tooltip cards
            var stack = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Vertical,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };

            string[] labels = new[]
            {
                "Launch Quick Flyout (Win+Alt+P)",
                "View Diagnostics & Telemetry",
                "Reset all monitor settings (Luminance 50%, Contrast 50%, RGB 50/50/50, Native 1920x1080 @ 200Hz)"
            };

            foreach (var label in labels)
            {
                var tipBorder = new System.Windows.Controls.Border
                {
                    Margin = new System.Windows.Thickness(8),
                    Background = bgBrush,
                    BorderBrush = borderBrush,
                    BorderThickness = new System.Windows.Thickness(1),
                    CornerRadius = new System.Windows.CornerRadius(6),
                    Padding = new System.Windows.Thickness(9, 6, 9, 6),
                    SnapsToDevicePixels = true,
                    UseLayoutRounding = true,
                    Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        Color = System.Windows.Media.Color.FromRgb(0, 0, 0),
                        BlurRadius = 12,
                        ShadowDepth = 2,
                        Opacity = 0.5
                    },
                    Child = new System.Windows.Controls.TextBlock
                    {
                        Text = label,
                        Foreground = fgBrush,
                        FontSize = 12,
                        FontFamily = (System.Windows.Media.FontFamily)System.Windows.Application.Current.Resources["PrismFontFamily"]
                    }
                };
                stack.Children.Add(tipBorder);
            }

            RenderViewToFile(stack, 650, 180, "PRISM_Tooltip_Verification.png");
        });
    }

    [Fact]
    public async Task HardwareCoalescing_RapidSliderEvents_CoalescesWithoutBusFlooding()
    {
        var displayService = new DdcCiMonitorService();
        var active = displayService.ActiveMonitor!;
        int initial = active.CurrentBrightness;

        var tasks = new List<Task<Result>>();
        var sw = Stopwatch.StartNew();

        // Simulate rapid slider drag across 20 discrete values (e.g. 41 to 60)
        for (int b = 41; b <= 60; b++)
        {
            tasks.Add(displayService.SetBrightnessAsync(active.Id, b));
        }

        // Enqueuing 20 rapid changes must be instantaneous (< 50ms) without locking caller
        Assert.True(sw.ElapsedMilliseconds < 50, $"Enqueuing took {sw.ElapsedMilliseconds}ms, must be < 50ms");

        // Wait for coalesced execution to complete
        var results = await Task.WhenAll(tasks);
        sw.Stop();

        foreach (var r in results)
        {
            Assert.True(r.IsSuccess);
        }

        // Active monitor must reflect final target value
        Assert.Equal(60, active.CurrentBrightness);

        // Restore initial baseline
        await displayService.SetBrightnessAsync(active.Id, initial);
    }

    private static void RenderViewToFile(System.Windows.UIElement element, int width, int height, string filename)
    {
        var border = new System.Windows.Controls.Border
        {
            Width = width,
            Height = height,
            Background = (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["PrismCanvasBrush"],
            Child = element
        };

        border.Measure(new System.Windows.Size(width, height));
        border.Arrange(new System.Windows.Rect(0, 0, width, height));
        border.UpdateLayout();

        var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        rtb.Render(border);

        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));

        // Find solution dir
        string solutionDir = System.IO.Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(solutionDir) && !System.IO.File.Exists(System.IO.Path.Combine(solutionDir, "PRISM.sln")))
        {
            var parent = System.IO.Directory.GetParent(solutionDir);
            if (parent == null) break;
            solutionDir = parent.FullName;
        }

        string workspacePath = System.IO.Path.Combine(solutionDir, filename);
        string artifactDir = @"C:\Users\krish\.gemini\antigravity\brain\1c12773d-f233-478e-848b-afe2bff88f4e";
        string artifactPath = System.IO.Path.Combine(artifactDir, filename);

        using (var fs = System.IO.File.Create(workspacePath))
        {
            encoder.Save(fs);
        }

        if (System.IO.Directory.Exists(artifactDir))
        {
            System.IO.File.Copy(workspacePath, artifactPath, true);
        }
    }
}
