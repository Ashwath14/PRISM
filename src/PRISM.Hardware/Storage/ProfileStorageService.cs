using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using PRISM.Core.Common;
using PRISM.Core.Interfaces;
using PRISM.Core.Models;
using Path = System.IO.Path;
using File = System.IO.File;
using Directory = System.IO.Directory;

namespace PRISM.Hardware.Storage;

public class ProfileStorageService : IProfileService, IEnvironmentService
{
    private readonly string _storageDir;
    private readonly string _profilesPath;
    private readonly string _environmentsPath;
    private readonly string _settingsPath;
    private readonly string _monitorPreferencesPath;
    private readonly string _headphoneProfilesPath;

    private readonly List<DisplayProfile> _displayProfiles = new();
    private readonly List<AudioProfile> _audioProfiles = new();
    private readonly List<EnvironmentPreset> _environments = new();
    private readonly List<HeadphoneProfile> _headphoneProfiles = new();
    private readonly Dictionary<string, MonitorPreference> _monitorPreferences = new();
    private EnvironmentPreset? _activePreset;
    private AppSettings _settings = new();

    private readonly object _lock = new();

    public event EventHandler<EnvironmentApplicationResult>? EnvironmentApplied;

    public ProfileStorageService(string? customStorageDir = null)
    {
        _storageDir = customStorageDir ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PRISM");
        _profilesPath = Path.Combine(_storageDir, "profiles.json");
        _environmentsPath = Path.Combine(_storageDir, "environments.json");
        _settingsPath = Path.Combine(_storageDir, "settings.json");
        _monitorPreferencesPath = Path.Combine(_storageDir, "monitor_preferences.json");
        _headphoneProfilesPath = Path.Combine(_storageDir, "headphone_profiles.json");

        InitializeDefaults();
        LoadFromDisk();
    }

    private void InitializeDefaults()
    {
        InitializeDisplayDefaults();
        InitializeAudioDefaults();
        InitializeEnvironmentDefaults();
        InitializeHeadphoneDefaults();
    }

    private void InitializeHeadphoneDefaults()
    {
        _headphoneProfiles.Clear();
        _headphoneProfiles.AddRange(new[]
        {
            new HeadphoneProfile
            {
                Id = "hp_hd600",
                Brand = "Sennheiser",
                Model = "HD 600 / HD 650",
                TargetCurve = "Harman Over-Ear 2018",
                Source = "AutoEq / Oratory1990",
                Description = "Acoustic calibration to Harman Target: sub-bass extension below 80 Hz with transparent mids.",
                PreampDb = -3.5,
                ParametricFilters = new List<ParametricFilter>
                {
                    new ParametricFilter { Frequency = 30, GainDb = 4.5, Q = 1.0, Type = ParametricFilterType.LowShelf, IsEnabled = true },
                    new ParametricFilter { Frequency = 85, GainDb = 2.0, Q = 0.7, Type = ParametricFilterType.Peaking, IsEnabled = true },
                    new ParametricFilter { Frequency = 3200, GainDb = -1.5, Q = 2.0, Type = ParametricFilterType.Peaking, IsEnabled = true },
                    new ParametricFilter { Frequency = 5800, GainDb = 2.5, Q = 1.8, Type = ParametricFilterType.Peaking, IsEnabled = true }
                }
            },
            new HeadphoneProfile
            {
                Id = "hp_m50x",
                Brand = "Audio-Technica",
                Model = "ATH-M50x",
                TargetCurve = "Harman Over-Ear 2018",
                Source = "AutoEq / Oratory1990",
                Description = "Acoustic calibration to Harman Target: smooths boomy 180 Hz resonance and tames 10 kHz peak.",
                PreampDb = -4.0,
                ParametricFilters = new List<ParametricFilter>
                {
                    new ParametricFilter { Frequency = 180, GainDb = -3.5, Q = 1.2, Type = ParametricFilterType.Peaking, IsEnabled = true },
                    new ParametricFilter { Frequency = 350, GainDb = 2.0, Q = 1.4, Type = ParametricFilterType.Peaking, IsEnabled = true },
                    new ParametricFilter { Frequency = 10000, GainDb = -4.0, Q = 2.5, Type = ParametricFilterType.Peaking, IsEnabled = true }
                }
            },
            new HeadphoneProfile
            {
                Id = "hp_xm4",
                Brand = "Sony",
                Model = "WH-1000XM4 / XM5",
                TargetCurve = "Harman Over-Ear 2018",
                Source = "AutoEq / Oratory1990",
                Description = "Acoustic calibration to Harman Target: cleans up bloated mid-bass and elevates vocal presence.",
                PreampDb = -5.0,
                ParametricFilters = new List<ParametricFilter>
                {
                    new ParametricFilter { Frequency = 150, GainDb = -4.5, Q = 0.9, Type = ParametricFilterType.Peaking, IsEnabled = true },
                    new ParametricFilter { Frequency = 1200, GainDb = 2.5, Q = 1.5, Type = ParametricFilterType.Peaking, IsEnabled = true },
                    new ParametricFilter { Frequency = 6500, GainDb = 3.0, Q = 2.0, Type = ParametricFilterType.Peaking, IsEnabled = true }
                }
            },
            new HeadphoneProfile
            {
                Id = "hp_airpods_max",
                Brand = "Apple",
                Model = "AirPods Max",
                TargetCurve = "Harman Over-Ear 2018",
                Source = "AutoEq / Crinacle",
                Description = "Acoustic calibration to Harman Target: natural treble sparkle and smooth upper midrange.",
                PreampDb = -3.0,
                ParametricFilters = new List<ParametricFilter>
                {
                    new ParametricFilter { Frequency = 2400, GainDb = 2.0, Q = 1.4, Type = ParametricFilterType.Peaking, IsEnabled = true },
                    new ParametricFilter { Frequency = 4800, GainDb = -2.5, Q = 2.0, Type = ParametricFilterType.Peaking, IsEnabled = true },
                    new ParametricFilter { Frequency = 9500, GainDb = 1.5, Q = 1.5, Type = ParametricFilterType.Peaking, IsEnabled = true }
                }
            },
            new HeadphoneProfile
            {
                Id = "hp_dt770",
                Brand = "Beyerdynamic",
                Model = "DT 770 / DT 990 Pro",
                TargetCurve = "Harman Over-Ear 2018",
                Source = "AutoEq / Oratory1990",
                Description = "Acoustic calibration to Harman Target: attenuates sharp 6-8 kHz Beyer peak for fatigue-free monitoring.",
                PreampDb = -4.5,
                ParametricFilters = new List<ParametricFilter>
                {
                    new ParametricFilter { Frequency = 220, GainDb = -2.0, Q = 1.2, Type = ParametricFilterType.Peaking, IsEnabled = true },
                    new ParametricFilter { Frequency = 5900, GainDb = -5.0, Q = 3.0, Type = ParametricFilterType.Peaking, IsEnabled = true },
                    new ParametricFilter { Frequency = 8500, GainDb = -4.0, Q = 2.5, Type = ParametricFilterType.Peaking, IsEnabled = true }
                }
            },
            new HeadphoneProfile
            {
                Id = "hp_iem_harman",
                Brand = "Reference",
                Model = "Harman In-Ear Target",
                TargetCurve = "Harman In-Ear 2019",
                Source = "AutoEq Reference",
                Description = "Universal acoustic target for in-ear monitors (IEMs) with smooth pinna gain and sub-bass shelf.",
                PreampDb = -3.5,
                ParametricFilters = new List<ParametricFilter>
                {
                    new ParametricFilter { Frequency = 25, GainDb = 3.5, Q = 0.8, Type = ParametricFilterType.LowShelf, IsEnabled = true },
                    new ParametricFilter { Frequency = 3000, GainDb = 2.5, Q = 1.5, Type = ParametricFilterType.Peaking, IsEnabled = true },
                    new ParametricFilter { Frequency = 7500, GainDb = -2.0, Q = 2.0, Type = ParametricFilterType.Peaking, IsEnabled = true }
                }
            }
        });
    }

    public static List<DisplayProfile> GetDefaultDisplayProfiles() => new()
    {
        new DisplayProfile 
        { 
            Id = "disp_daylight", 
            Name = "Daylight Focus", 
            Description = "Balanced daytime productivity with natural clarity and neutral color",
            Category = "Productivity",
            Brightness = 55, 
            Contrast = 50, 
            Sharpness = 50,
            RedGain = 50, 
            GreenGain = 50, 
            BlueGain = 50,
            Saturation = 50,
            Gamma = 2.2,
            ColorTemperature = "Neutral (6500K)",
            SoftwareDimmingEnabled = false,
            SoftwareDimmingLevel = 0,
            IsBuiltIn = true,
            IsCustom = false
        },
        new DisplayProfile 
        { 
            Id = "disp_latenight", 
            Name = "Late Night", 
            Description = "Comfortable low-light viewing with warm tone to reduce eye fatigue",
            Category = "Relaxation",
            Brightness = 25, 
            Contrast = 48, 
            Sharpness = 50,
            RedGain = 52, 
            GreenGain = 50, 
            BlueGain = 46,
            Saturation = 50,
            Gamma = 2.2,
            ColorTemperature = "Warm (5500K)",
            SoftwareDimmingEnabled = false,
            SoftwareDimmingLevel = 0,
            IsBuiltIn = true,
            IsCustom = false
        },
        new DisplayProfile 
        { 
            Id = "disp_gaming", 
            Name = "Competitive Gaming", 
            Description = "High visibility and enhanced shadow clarity for competitive titles",
            Category = "Gaming",
            Brightness = 75, 
            Contrast = 52, 
            Sharpness = 50,
            RedGain = 50, 
            GreenGain = 50, 
            BlueGain = 50,
            Saturation = 50,
            Gamma = 2.2,
            ColorTemperature = "Neutral (6500K)",
            SoftwareDimmingEnabled = false,
            SoftwareDimmingLevel = 0,
            IsBuiltIn = true,
            IsCustom = false
        },
        new DisplayProfile 
        { 
            Id = "disp_cinema", 
            Name = "Cinematic Movie", 
            Description = "Deep blacks and vibrant theatrical contrast for movies and streaming",
            Category = "Cinema",
            Brightness = 60, 
            Contrast = 55, 
            Sharpness = 50,
            RedGain = 52, 
            GreenGain = 50, 
            BlueGain = 48,
            Saturation = 50,
            Gamma = 2.2,
            ColorTemperature = "Slightly Warm (6000K)",
            SoftwareDimmingEnabled = false,
            SoftwareDimmingLevel = 0,
            IsBuiltIn = true,
            IsCustom = false
        }
    };

    private void InitializeDisplayDefaults()
    {
        _displayProfiles.Clear();
        _displayProfiles.AddRange(GetDefaultDisplayProfiles());
    }

    private void InitializeAudioDefaults()
    {
        _audioProfiles.Clear();
        _audioProfiles.AddRange(new[]
        {
            new AudioProfile 
            { 
                Id = "aud_flat", 
                Name = "Flat / Reference", 
                Category = "Audiophile / Music",
                Description = "Pure, uncolored bit-exact reference monitoring across all 10 bands.",
                IntendedExperience = "Studio master monitoring, analytical listening, and pure source playback.",
                BandGains = new double[10],
                BassGainDb = 0.0,
                TrebleGainDb = 0.0,
                PreampDb = 0.0,
                IsLoudnessNormalizationEnabled = true
            },
            new AudioProfile 
            { 
                Id = "aud_deep_bass", 
                Name = "Deep Bass", 
                Category = "Audiophile / Music",
                Description = "Massive low-end extension focused on sub-bass frequencies below 60 Hz.",
                IntendedExperience = "Electronic, Synthwave, Hip-Hop, and visceral bass immersion.",
                BandGains = new double[] { 7.0, 5.5, 3.0, 1.0, 0.0, 0.0, 0.0, 0.5, 1.0, 1.0 },
                BassGainDb = 4.5,
                PreampDb = -5.0,
                IsLoudnessNormalizationEnabled = true
            },
            new AudioProfile 
            { 
                Id = "aud_punchy_bass", 
                Name = "Punchy Bass", 
                Category = "Audiophile / Music",
                Description = "Fast, impactful kick drum and bassline energy between 60 Hz and 150 Hz.",
                IntendedExperience = "EDM, Dance, Funk, and high-energy modern pop with tight transient impact.",
                BandGains = new double[] { 3.5, 6.0, 4.5, 1.5, 0.0, -0.5, 0.0, 1.0, 1.5, 1.0 },
                BassGainDb = 3.5,
                PreampDb = -4.0,
                IsLoudnessNormalizationEnabled = true
            },
            new AudioProfile 
            { 
                Id = "aud_clear_bass", 
                Name = "Clear Bass", 
                Category = "Audiophile / Music",
                Description = "Deep resonant bass without low-mid mud, dipping 250-500 Hz for clarity.",
                IntendedExperience = "Warm bass richness with articulate instrument separation and vocal space.",
                BandGains = new double[] { 4.5, 4.5, 2.0, -1.5, -1.0, 0.0, 1.0, 2.0, 2.5, 2.5 },
                BassGainDb = 2.5,
                TrebleGainDb = 1.5,
                PreampDb = -3.5,
                IsLoudnessNormalizationEnabled = true
            },
            new AudioProfile 
            { 
                Id = "aud_balanced", 
                Name = "Balanced Music", 
                Category = "Audiophile / Music",
                Description = "Gentle musical V-curve boosting low warmth and upper harmonic presence.",
                IntendedExperience = "Universal daily listening profile for all genres with rich, engaging tone.",
                BandGains = new double[] { 3.0, 2.5, 1.5, 0.5, 0.0, 0.0, 0.5, 1.5, 2.0, 2.0 }, 
                BassGainDb = 1.5,
                TrebleGainDb = 1.5,
                PreampDb = -2.5,
                IsLoudnessNormalizationEnabled = true
            },
            new AudioProfile 
            { 
                Id = "aud_vocal", 
                Name = "Vocal Clarity", 
                Category = "Voice & Podcast",
                Description = "Forward 1 kHz - 3 kHz tuning that elevates voices and speech intelligibility.",
                IntendedExperience = "Audiobooks, podcasts, YouTube commentary, and vocal-centric tracks.",
                BandGains = new double[] { -2.5, -1.5, -0.5, 1.0, 2.5, 4.5, 3.5, 1.5, 0.5, 0.0 }, 
                IsCompressorEnabled = true, 
                CompressorThresholdDb = -14.0,
                CompressorRatio = 2.5,
                PreampDb = -3.5,
                IsLoudnessNormalizationEnabled = true
            },
            new AudioProfile 
            { 
                Id = "aud_acoustic", 
                Name = "Acoustic", 
                Category = "Audiophile / Music",
                Description = "Natural string, wood, and guitar body resonance with open treble air.",
                IntendedExperience = "Unplugged sessions, singer-songwriter, folk, and classical guitar.",
                BandGains = new double[] { 1.5, 1.5, 1.0, 0.0, 0.5, 1.0, 2.0, 3.0, 3.5, 4.0 }, 
                TrebleGainDb = 2.0,
                PreampDb = -2.5,
                IsLoudnessNormalizationEnabled = true
            },
            new AudioProfile 
            { 
                Id = "aud_classical", 
                Name = "Classical", 
                Category = "Audiophile / Music",
                Description = "Expansive orchestral dynamic range, smooth brass, and airy string shimmer.",
                IntendedExperience = "Symphony, chamber music, piano solos, and high dynamic range recordings.",
                BandGains = new double[] { 1.5, 1.5, 1.0, 0.5, 0.0, 0.0, 0.5, 1.5, 2.5, 3.0 }, 
                PreampDb = -2.0,
                IsLoudnessNormalizationEnabled = true
            },
            new AudioProfile 
            { 
                Id = "aud_rock", 
                Name = "Rock", 
                Category = "Audiophile / Music",
                Description = "Heavy bass drum thump, scooped mids, and aggressive guitar bite at 2k-4k.",
                IntendedExperience = "Hard rock, metal, grunge, and energetic guitar riffs.",
                BandGains = new double[] { 5.5, 4.5, 2.5, 0.0, -1.5, 0.5, 2.5, 4.0, 4.5, 5.0 }, 
                BassGainDb = 3.0,
                TrebleGainDb = 2.5,
                PreampDb = -4.5,
                IsLoudnessNormalizationEnabled = true
            },
            new AudioProfile 
            { 
                Id = "aud_pop", 
                Name = "Pop", 
                Category = "Audiophile / Music",
                Description = "Radio-ready mastering curve with tight bass and sparkling vocal presence.",
                IntendedExperience = "Chart hits, streaming playlists, and modern electronic pop.",
                BandGains = new double[] { 4.5, 5.0, 3.0, 1.0, -0.5, 0.5, 2.0, 3.5, 4.0, 4.0 }, 
                BassGainDb = 2.5,
                TrebleGainDb = 2.0,
                PreampDb = -4.0,
                IsLoudnessNormalizationEnabled = true
            },
            new AudioProfile 
            { 
                Id = "aud_electronic", 
                Name = "Electronic", 
                Category = "Audiophile / Music",
                Description = "Deep club rumble and sizzling synthesizer highs with stereo widening.",
                IntendedExperience = "House, Techno, Trance, Dubstep, and ambient synth soundscapes.",
                BandGains = new double[] { 7.0, 6.0, 3.5, 0.5, -0.5, 0.5, 2.5, 4.0, 5.5, 6.0 }, 
                BassGainDb = 4.0,
                TrebleGainDb = 3.5,
                IsStereoWideningEnabled = true,
                StereoWidthPercent = 130,
                PreampDb = -5.5,
                IsLoudnessNormalizationEnabled = true
            },
            new AudioProfile 
            { 
                Id = "aud_cinema", 
                Name = "Movie / Cinema", 
                Category = "Cinema & Movies",
                Description = "Theatrical rumble for explosions and sub effects with crystal clear dialogue.",
                IntendedExperience = "Feature films, blockbuster trailers, and cinematic gaming.",
                BandGains = new double[] { 6.0, 5.0, 2.5, 0.0, -1.0, 1.5, 2.5, 3.5, 4.5, 5.0 }, 
                BassGainDb = 3.5,
                TrebleGainDb = 3.0,
                IsStereoWideningEnabled = true,
                StereoWidthPercent = 150,
                PreampDb = -4.5,
                IsLoudnessNormalizationEnabled = true
            },
            new AudioProfile 
            { 
                Id = "aud_game", 
                Name = "Gaming / Footsteps", 
                Category = "Competitive Gaming",
                Description = "Tactical audio scoop attenuating rumble while boosting footstep frequencies.",
                IntendedExperience = "FPS titles (CS2, Valorant, Apex, Warzone) for spatial positioning awareness.",
                BandGains = new double[] { -4.0, -2.5, -0.5, 1.0, 2.0, 4.5, 5.5, 4.5, 2.5, 1.0 }, 
                IsStereoWideningEnabled = true,
                StereoWidthPercent = 140,
                PreampDb = -3.5,
                IsLoudnessNormalizationEnabled = true
            },
            new AudioProfile 
            { 
                Id = "aud_voice", 
                Name = "Voice / Podcast", 
                Category = "Voice & Podcast",
                Description = "Warm proximity effect with de-essed highs and focused vocal clarity.",
                IntendedExperience = "Discord voice calls, Zoom meetings, lectures, and interview podcasts.",
                BandGains = new double[] { -4.5, -3.0, -1.0, 1.5, 3.0, 4.0, 3.0, 1.0, -0.5, -2.0 }, 
                IsCompressorEnabled = true, 
                CompressorThresholdDb = -16.0,
                CompressorRatio = 3.0,
                PreampDb = -3.0,
                IsLoudnessNormalizationEnabled = true
            },
            new AudioProfile 
            { 
                Id = "aud_night", 
                Name = "Night Listening", 
                Category = "Cinema & Movies",
                Description = "Tamed dynamic range and rolled-off sub-bass to prevent family disturbance.",
                IntendedExperience = "Late-night movies and gaming at low master volume without missing quiet details.",
                BandGains = new double[] { -5.0, -4.0, -2.0, 0.0, 1.0, 2.0, 1.5, 0.5, -1.5, -3.5 }, 
                IsCompressorEnabled = true, 
                CompressorThresholdDb = -18.0, 
                CompressorRatio = 4.0,
                PreampDb = -2.5,
                IsLoudnessNormalizationEnabled = true
            },
            new AudioProfile 
            { 
                Id = "aud_loudness", 
                Name = "Loudness Balanced", 
                Category = "Audiophile / Music",
                Description = "Fletcher-Munson equal loudness compensation for full, rich sound at low volume.",
                IntendedExperience = "Background music at modest listening levels without thin bass or dull highs.",
                BandGains = new double[] { 5.0, 4.0, 2.0, 0.0, -0.5, 0.0, 1.0, 2.0, 3.5, 4.5 }, 
                BassGainDb = 2.5,
                TrebleGainDb = 2.5,
                PreampDb = -4.0,
                IsLoudnessNormalizationEnabled = true
            }
        });
    }

    private void InitializeEnvironmentDefaults()
    {
        _environments.Clear();
        _environments.AddRange(new[]
        {
            new EnvironmentPreset 
            { 
                Id = "env_day", 
                Name = "Daylight Focus", 
                Description = "Balanced natural brightness and neutral sound for daytime productivity", 
                IconKey = "SunIcon", 
                ImagePath = "pack://application:,,,/PRISM;component/Assets/Environments/daylight_focus.jpg",
                IsActive = true, 
                DisplaySettings = new DisplayProfile 
                { 
                    Id = "disp_daylight", 
                    Name = "Daylight Focus", 
                    Brightness = 55, 
                    Contrast = 50, 
                    RedGain = 50, 
                    GreenGain = 50, 
                    BlueGain = 50, 
                    IsSubZeroDimEnabled = false,
                    SubZeroDimPercent = 0
                }, 
                AudioSettings = new AudioProfile 
                { 
                    Id = "aud_flat", 
                    Name = "Flat (Reference)", 
                    Category = "Audiophile / Music",
                    Description = "Pure, uncolored bit-exact reference monitoring across all 10 bands.",
                    IntendedExperience = "Studio master monitoring, analytical listening, and pure source playback.",
                    BandGains = new double[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                    PreampDb = 0,
                    IsLoudnessNormalizationEnabled = true
                }
            },
            new EnvironmentPreset 
            { 
                Id = "env_night", 
                Name = "Late Night", 
                Description = "Comfortable low-light viewing with dialogue compression and reduced strain", 
                IconKey = "MoonIcon", 
                ImagePath = "pack://application:,,,/PRISM;component/Assets/Environments/late_night.jpg",
                DisplaySettings = new DisplayProfile 
                { 
                    Id = "disp_latenight", 
                    Name = "Late Night Dim", 
                    Brightness = 20, 
                    Contrast = 45, 
                    RedGain = 52, 
                    GreenGain = 50, 
                    BlueGain = 46, 
                    IsSubZeroDimEnabled = false,
                    SubZeroDimPercent = 0
                }, 
                AudioSettings = new AudioProfile 
                { 
                    Id = "aud_night", 
                    Name = "Night Listening", 
                    Category = "Cinema & Movies",
                    Description = "Tamed dynamic range and rolled-off sub-bass to prevent disturbance while preserving dialogue.",
                    IntendedExperience = "Late-night movies and gaming at low master volume without missing quiet details.",
                    BandGains = new double[] { -5.0, -4.0, -2.0, 0.0, 1.0, 2.0, 1.5, 0.5, -1.5, -3.5 }, 
                    IsCompressorEnabled = true, 
                    CompressorThresholdDb = -18.0, 
                    CompressorRatio = 4.0,
                    PreampDb = -2.5,
                    IsLoudnessNormalizationEnabled = true
                }
            },
            new EnvironmentPreset 
            { 
                Id = "env_game", 
                Name = "Competitive Gaming", 
                Description = "High visibility shadow clarity with footstep audio positioning", 
                IconKey = "GamepadIcon", 
                ImagePath = "pack://application:,,,/PRISM;component/Assets/Environments/competitive_gaming.jpg",
                DisplaySettings = new DisplayProfile 
                { 
                    Id = "disp_gaming", 
                    Name = "Competitive Gaming", 
                    Brightness = 80, 
                    Contrast = 60, 
                    RedGain = 50, 
                    GreenGain = 50, 
                    BlueGain = 50, 
                    IsSubZeroDimEnabled = false,
                    SubZeroDimPercent = 0
                }, 
                AudioSettings = new AudioProfile 
                { 
                    Id = "aud_game", 
                    Name = "Gaming / Footsteps", 
                    Category = "Competitive Gaming",
                    Description = "Tactical audio scoop attenuating low rumble while boosting footstep frequencies (2k-4k).",
                    IntendedExperience = "FPS titles (CS2, Valorant, Apex, Warzone) for spatial positioning awareness.",
                    BandGains = new double[] { -4.0, -2.5, -0.5, 1.0, 2.0, 4.5, 5.5, 4.5, 2.5, 1.0 }, 
                    IsStereoWideningEnabled = true,
                    StereoWidthPercent = 125,
                    PreampDb = -3.5,
                    IsLoudnessNormalizationEnabled = true
                }
            },
            new EnvironmentPreset 
            { 
                Id = "env_cinema", 
                Name = "Cinematic Movie", 
                Description = "Vivid dynamic display with immersive cinematic soundstage", 
                IconKey = "FilmIcon", 
                ImagePath = "pack://application:,,,/PRISM;component/Assets/Environments/cinematic_movie.jpg",
                DisplaySettings = new DisplayProfile 
                { 
                    Id = "disp_cinema", 
                    Name = "Cinematic Movie", 
                    Brightness = 65, 
                    Contrast = 56, 
                    RedGain = 52, 
                    GreenGain = 50, 
                    BlueGain = 48, 
                    IsSubZeroDimEnabled = false,
                    SubZeroDimPercent = 0
                }, 
                AudioSettings = new AudioProfile 
                { 
                    Id = "aud_cinema", 
                    Name = "Movie / Cinema", 
                    Category = "Cinema & Movies",
                    Description = "Theatrical rumble for cinematic impact with crystal clear dialogue articulation.",
                    IntendedExperience = "Feature films, streaming media, and cinematic experiences.",
                    BandGains = new double[] { 6.0, 5.0, 2.5, 0.0, -1.0, 1.5, 2.5, 3.5, 4.5, 5.0 }, 
                    BassGainDb = 3.5,
                    TrebleGainDb = 3.0,
                    IsStereoWideningEnabled = true,
                    StereoWidthPercent = 140,
                    PreampDb = -4.5,
                    IsLoudnessNormalizationEnabled = true
                }
            }
        });

        _activePreset = _environments.Count > 0 ? _environments[0] : null;
    }

    private void LoadFromDisk()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(_profilesPath))
                {
                    var json = File.ReadAllText(_profilesPath);
                    var data = JsonSerializer.Deserialize<ProfileContainer>(json);
                    if (data != null)
                    {
                        if (data.DisplayProfiles?.Count > 0)
                        {
                            foreach (var p in data.DisplayProfiles)
                            {
                                var idx = _displayProfiles.FindIndex(d => d.Id == p.Id);
                                if (idx >= 0) _displayProfiles[idx] = p;
                                else _displayProfiles.Add(p);
                            }
                        }
                        if (data.AudioProfiles?.Count > 0)
                        {
                            foreach (var p in data.AudioProfiles)
                            {
                                var idx = _audioProfiles.FindIndex(d => d.Id == p.Id);
                                if (idx >= 0) _audioProfiles[idx] = p;
                                else _audioProfiles.Add(p);
                            }
                        }
                    }
                }

                SanitizeDisplayProfiles();

                if (File.Exists(_environmentsPath))
                {
                    var json = File.ReadAllText(_environmentsPath);
                    var envs = JsonSerializer.Deserialize<List<EnvironmentPreset>>(json);
                    if (envs?.Count > 0)
                    {
                        foreach (var e in envs)
                        {
                            var idx = _environments.FindIndex(item => item.Id == e.Id);
                            if (idx >= 0) _environments[idx] = e;
                            else _environments.Add(e);
                        }

                        // Sanitize and heal environment presets from stale or corrupted disk files
                        bool needsResave = false;
                        var gameEnv = _environments.FirstOrDefault(item => item.Id == "env_game");
                        if (gameEnv != null && (gameEnv.DisplaySettings.Brightness <= 15 || 
                                               (gameEnv.DisplaySettings.Name != null && gameEnv.DisplaySettings.Name.Contains("Night"))))
                        {
                            gameEnv.DisplaySettings.Name = "Competitive Gaming";
                            gameEnv.DisplaySettings.Brightness = 80;
                            gameEnv.DisplaySettings.Contrast = 60;
                            gameEnv.DisplaySettings.RedGain = 50;
                            gameEnv.DisplaySettings.GreenGain = 50;
                            gameEnv.DisplaySettings.BlueGain = 50;
                            gameEnv.DisplaySettings.IsSubZeroDimEnabled = false;
                            gameEnv.DisplaySettings.SubZeroDimPercent = 0;
                            gameEnv.AudioSettings.Name = "Gaming / Footsteps";
                            gameEnv.AudioSettings.Category = "Competitive Gaming";
                            gameEnv.AudioSettings.Description = "Tactical audio scoop attenuating low rumble while boosting footstep frequencies (2k-4k).";
                            gameEnv.AudioSettings.BandGains = new double[] { -4.0, -2.5, -0.5, 1.0, 2.0, 4.5, 5.5, 4.5, 2.5, 1.0 };
                            gameEnv.AudioSettings.IsStereoWideningEnabled = true;
                            gameEnv.AudioSettings.StereoWidthPercent = 125;
                            gameEnv.AudioSettings.PreampDb = -3.5;
                            needsResave = true;
                        }

                        var dayEnv = _environments.FirstOrDefault(item => item.Id == "env_day");
                        if (dayEnv != null && (dayEnv.DisplaySettings.Name != null && dayEnv.DisplaySettings.Name.Contains("Vivid")))
                        {
                            dayEnv.DisplaySettings.Name = "Daylight Focus";
                            dayEnv.DisplaySettings.Brightness = 55;
                            dayEnv.DisplaySettings.Contrast = 50;
                            dayEnv.DisplaySettings.RedGain = 50;
                            dayEnv.DisplaySettings.GreenGain = 50;
                            dayEnv.DisplaySettings.BlueGain = 50;
                            dayEnv.DisplaySettings.IsSubZeroDimEnabled = false;
                            dayEnv.DisplaySettings.SubZeroDimPercent = 0;
                            needsResave = true;
                        }

                        var nightEnv = _environments.FirstOrDefault(item => item.Id == "env_night");
                        if (nightEnv != null && (nightEnv.DisplaySettings.Name != null && nightEnv.DisplaySettings.Name.Contains("Reading")))
                        {
                            nightEnv.DisplaySettings.Name = "Late Night Dim";
                            nightEnv.DisplaySettings.Brightness = 20;
                            nightEnv.DisplaySettings.Contrast = 45;
                            nightEnv.DisplaySettings.RedGain = 52;
                            nightEnv.DisplaySettings.GreenGain = 50;
                            nightEnv.DisplaySettings.BlueGain = 46;
                            nightEnv.DisplaySettings.IsSubZeroDimEnabled = false;
                            nightEnv.DisplaySettings.SubZeroDimPercent = 0;
                            needsResave = true;
                        }

                        var cinemaEnv = _environments.FirstOrDefault(item => item.Id == "env_cinema");
                        if (cinemaEnv != null && (cinemaEnv.DisplaySettings.Name != null && cinemaEnv.DisplaySettings.Name.Contains("Standard")))
                        {
                            cinemaEnv.DisplaySettings.Name = "Cinematic Movie";
                            cinemaEnv.DisplaySettings.Brightness = 65;
                            cinemaEnv.DisplaySettings.Contrast = 56;
                            cinemaEnv.DisplaySettings.RedGain = 52;
                            cinemaEnv.DisplaySettings.GreenGain = 50;
                            cinemaEnv.DisplaySettings.BlueGain = 48;
                            cinemaEnv.DisplaySettings.IsSubZeroDimEnabled = false;
                            cinemaEnv.DisplaySettings.SubZeroDimPercent = 0;
                            cinemaEnv.AudioSettings.Name = "Movie / Cinema";
                            cinemaEnv.AudioSettings.Category = "Cinema & Movies";
                            cinemaEnv.AudioSettings.Description = "Theatrical rumble for cinematic impact with crystal clear dialogue articulation.";
                            cinemaEnv.AudioSettings.BandGains = new double[] { 6.0, 5.0, 2.5, 0.0, -1.0, 1.5, 2.5, 3.5, 4.5, 5.0 };
                            cinemaEnv.AudioSettings.BassGainDb = 3.5;
                            cinemaEnv.AudioSettings.TrebleGainDb = 3.0;
                            cinemaEnv.AudioSettings.IsStereoWideningEnabled = true;
                            cinemaEnv.AudioSettings.StereoWidthPercent = 140;
                            cinemaEnv.AudioSettings.PreampDb = -4.5;
                            needsResave = true;
                        }

                        if (needsResave)
                        {
                            SaveToDisk();
                        }
                    }
                }

                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    var s = JsonSerializer.Deserialize<AppSettings>(json);
                    if (s != null) _settings = s;
                }

                if (File.Exists(_monitorPreferencesPath))
                {
                    var json = File.ReadAllText(_monitorPreferencesPath);
                    var prefs = JsonSerializer.Deserialize<List<MonitorPreference>>(json);
                    if (prefs != null)
                    {
                        _monitorPreferences.Clear();
                        foreach (var p in prefs)
                        {
                            _monitorPreferences[p.MonitorKey] = p;
                        }
                    }
                }

                if (File.Exists(_headphoneProfilesPath))
                {
                    var json = File.ReadAllText(_headphoneProfilesPath);
                    var hps = JsonSerializer.Deserialize<List<HeadphoneProfile>>(json);
                    if (hps?.Count > 0)
                    {
                        foreach (var hp in hps)
                        {
                            var idx = _headphoneProfiles.FindIndex(item => item.Id == hp.Id);
                            if (idx >= 0) _headphoneProfiles[idx] = hp;
                            else _headphoneProfiles.Add(hp);
                        }
                    }
                }

                var active = _environments.FirstOrDefault(e => e.Id == _settings.ActiveEnvironmentId) 
                             ?? _environments.FirstOrDefault(e => e.IsActive) 
                             ?? _environments.FirstOrDefault();

                if (active != null)
                {
                    foreach (var e in _environments) e.IsActive = (e.Id == active.Id);
                    _activePreset = active;
                }
            }
            catch { }
        }
    }

    private void SaveToDisk()
    {
        lock (_lock)
        {
            try
            {
                if (!Directory.Exists(_storageDir)) Directory.CreateDirectory(_storageDir);

                // 1. Save Profiles
                var container = new ProfileContainer
                {
                    DisplayProfiles = _displayProfiles,
                    AudioProfiles = _audioProfiles
                };
                var jsonProfiles = JsonSerializer.Serialize(container, new JsonSerializerOptions { WriteIndented = true });
                string tmpProfiles = _profilesPath + ".tmp";
                File.WriteAllText(tmpProfiles, jsonProfiles);
                File.Move(tmpProfiles, _profilesPath, true);

                // 2. Save Environments
                var jsonEnvs = JsonSerializer.Serialize(_environments, new JsonSerializerOptions { WriteIndented = true });
                string tmpEnvs = _environmentsPath + ".tmp";
                File.WriteAllText(tmpEnvs, jsonEnvs);
                File.Move(tmpEnvs, _environmentsPath, true);

                // 3. Save Settings
                var jsonSettings = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                string tmpSettings = _settingsPath + ".tmp";
                File.WriteAllText(tmpSettings, jsonSettings);
                File.Move(tmpSettings, _settingsPath, true);

                // 4. Save Monitor Preferences
                var jsonPrefs = JsonSerializer.Serialize(_monitorPreferences.Values.ToList(), new JsonSerializerOptions { WriteIndented = true });
                string tmpPrefs = _monitorPreferencesPath + ".tmp";
                File.WriteAllText(tmpPrefs, jsonPrefs);
                File.Move(tmpPrefs, _monitorPreferencesPath, true);

                // 5. Save Headphone Profiles
                var jsonHps = JsonSerializer.Serialize(_headphoneProfiles, new JsonSerializerOptions { WriteIndented = true });
                string tmpHps = _headphoneProfilesPath + ".tmp";
                File.WriteAllText(tmpHps, jsonHps);
                File.Move(tmpHps, _headphoneProfilesPath, true);
            }
            catch { }
        }
    }

    public IReadOnlyList<HeadphoneProfile> GetHeadphoneProfiles()
    {
        lock (_lock)
        {
            return _headphoneProfiles.ToList().AsReadOnly();
        }
    }

    public Task<Result> SaveHeadphoneProfileAsync(HeadphoneProfile profile)
    {
        lock (_lock)
        {
            var idx = _headphoneProfiles.FindIndex(p => p.Id == profile.Id);
            if (idx >= 0) _headphoneProfiles[idx] = profile;
            else _headphoneProfiles.Add(profile);
            SaveToDisk();
            return Task.FromResult(Result.Success());
        }
    }

    public Task<Result> DeleteHeadphoneProfileAsync(string profileId)
    {
        lock (_lock)
        {
            var target = _headphoneProfiles.FirstOrDefault(p => p.Id == profileId);
            if (target == null) return Task.FromResult(Result.Failure("Profile not found"));
            _headphoneProfiles.Remove(target);
            SaveToDisk();
            return Task.FromResult(Result.Success());
        }
    }

    public Task<Result<HeadphoneProfile>> ImportAutoEqProfileAsync(string filePath)
    {
        lock (_lock)
        {
            try
            {
                if (!File.Exists(filePath)) return Task.FromResult(Result<HeadphoneProfile>.Failure("File not found"));
                var lines = File.ReadAllLines(filePath);
                var profile = new HeadphoneProfile
                {
                    Id = "custom_hp_" + Guid.NewGuid().ToString("N")[..8],
                    Brand = "Custom",
                    Model = Path.GetFileNameWithoutExtension(filePath),
                    TargetCurve = "AutoEq Imported",
                    Source = "User Import",
                    Description = $"Imported from {Path.GetFileName(filePath)}",
                    IsCustom = true
                };

                foreach (var rawLine in lines)
                {
                    var line = rawLine.Trim();
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

                    if (line.StartsWith("Preamp:", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = line.Split(':', StringSplitOptions.TrimEntries);
                        if (parts.Length > 1)
                        {
                            var valStr = parts[1].Replace("dB", "", StringComparison.OrdinalIgnoreCase).Trim();
                            if (double.TryParse(valStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var pDb))
                            {
                                profile.PreampDb = pDb;
                            }
                        }
                    }
                    else if (line.StartsWith("Filter", StringComparison.OrdinalIgnoreCase))
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(line,
                            @"Filter\s*(?:\d+)?:\s*(?:(ON|OFF)\s+)?(PK|LSC|HSC|LP|HP|NO|Peaking|LowShelf|HighShelf)\s+Fc\s+([\d\.]+)\s*Hz\s+Gain\s+([-\d\.]+)\s*dB\s+Q\s+([\d\.]+)",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                        if (match.Success)
                        {
                            bool isEnabled = !match.Groups[1].Success || match.Groups[1].Value.Equals("ON", StringComparison.OrdinalIgnoreCase);
                            string typeStr = match.Groups[2].Value.ToUpperInvariant();
                            double freq = double.Parse(match.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);
                            double gain = double.Parse(match.Groups[4].Value, System.Globalization.CultureInfo.InvariantCulture);
                            double q = double.Parse(match.Groups[5].Value, System.Globalization.CultureInfo.InvariantCulture);

                            var filterType = typeStr switch
                            {
                                "LSC" or "LOWSHELF" => ParametricFilterType.LowShelf,
                                "HSC" or "HIGHSHELF" => ParametricFilterType.HighShelf,
                                "LP" or "LOWPASS" => ParametricFilterType.LowPass,
                                "HP" or "HIGHPASS" => ParametricFilterType.HighPass,
                                "NO" or "NOTCH" => ParametricFilterType.Notch,
                                _ => ParametricFilterType.Peaking
                            };

                            profile.ParametricFilters.Add(new ParametricFilter
                            {
                                IsEnabled = isEnabled,
                                Type = filterType,
                                Frequency = freq,
                                GainDb = gain,
                                Q = q
                            });
                        }
                    }
                }

                _headphoneProfiles.Add(profile);
                SaveToDisk();
                return Task.FromResult(Result<HeadphoneProfile>.Success(profile));
            }
            catch (Exception ex)
            {
                return Task.FromResult(Result<HeadphoneProfile>.Failure($"Failed to import AutoEq profile: {ex.Message}"));
            }
        }
    }

    public IReadOnlyList<DisplayProfile> GetDisplayProfiles() => _displayProfiles.AsReadOnly();
    public IReadOnlyList<AudioProfile> GetAudioProfiles() => _audioProfiles.AsReadOnly();

    public Task<Result> SaveDisplayProfileAsync(DisplayProfile profile)
    {
        lock (_lock)
        {
            var idx = _displayProfiles.FindIndex(p => p.Id == profile.Id);
            if (idx >= 0) _displayProfiles[idx] = profile;
            else _displayProfiles.Add(profile);
            SaveToDisk();
        }
        return Task.FromResult(Result.Success());
    }

    public Task<Result> SaveAudioProfileAsync(AudioProfile profile)
    {
        lock (_lock)
        {
            var idx = _audioProfiles.FindIndex(p => p.Id == profile.Id);
            if (idx >= 0) _audioProfiles[idx] = profile;
            else _audioProfiles.Add(profile);
            SaveToDisk();
        }
        return Task.FromResult(Result.Success());
    }

    private void SanitizeDisplayProfiles()
    {
        var legacyIds = new HashSet<string> { "disp_std", "disp_vivid", "disp_read", "disp_night", "disp_game", "disp_creator" };
        bool modified = false;

        if (_displayProfiles.Any(p => legacyIds.Contains(p.Id)))
        {
            _displayProfiles.RemoveAll(p => legacyIds.Contains(p.Id));
            modified = true;
        }

        var defaults = GetDefaultDisplayProfiles();
        foreach (var def in defaults)
        {
            var existing = _displayProfiles.FirstOrDefault(p => p.Id == def.Id);
            if (existing == null)
            {
                _displayProfiles.Add(def);
                modified = true;
            }
            else
            {
                if (existing.Id == "disp_gaming" && existing.Brightness <= 15)
                {
                    existing.Brightness = def.Brightness;
                    existing.Contrast = def.Contrast;
                    modified = true;
                }
                existing.IsBuiltIn = true;
                if (string.IsNullOrEmpty(existing.Category)) existing.Category = def.Category;
                if (string.IsNullOrEmpty(existing.Description)) existing.Description = def.Description;
            }
        }

        var order = new List<string> { "disp_daylight", "disp_latenight", "disp_gaming", "disp_cinema" };
        _displayProfiles.Sort((a, b) =>
        {
            int idxA = order.IndexOf(a.Id);
            int idxB = order.IndexOf(b.Id);
            if (idxA >= 0 && idxB >= 0) return idxA.CompareTo(idxB);
            if (idxA >= 0) return -1;
            if (idxB >= 0) return 1;
            return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });

        if (modified)
        {
            SaveToDisk();
        }
    }

    public Task<Result> DeleteDisplayProfileAsync(string profileId)
    {
        lock (_lock)
        {
            var target = _displayProfiles.FirstOrDefault(p => p.Id == profileId);
            if (target == null) return Task.FromResult(Result.Failure("Profile not found."));
            if (target.IsBuiltIn) return Task.FromResult(Result.Failure("Factory profiles cannot be deleted."));
            _displayProfiles.Remove(target);
            SaveToDisk();
        }
        return Task.FromResult(Result.Success());
    }

    public Task<Result> DeleteAudioProfileAsync(string profileId)
    {
        lock (_lock)
        {
            _audioProfiles.RemoveAll(p => p.Id == profileId);
            SaveToDisk();
        }
        return Task.FromResult(Result.Success());
    }

    public Task<Result<DisplayProfile>> CreateCustomDisplayProfileAsync(string name, int brightness, int contrast, int red, int green, int blue, bool subZero, int subZeroPct)
    {
        lock (_lock)
        {
            var profile = new DisplayProfile
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = string.IsNullOrWhiteSpace(name) ? "Custom Profile" : name.Trim(),
                IsCustom = true,
                Brightness = Math.Clamp(brightness, 0, 100),
                Contrast = Math.Clamp(contrast, 0, 100),
                RedGain = Math.Clamp(red, 0, 100),
                GreenGain = Math.Clamp(green, 0, 100),
                BlueGain = Math.Clamp(blue, 0, 100),
                IsSubZeroDimEnabled = subZero,
                SubZeroDimPercent = Math.Clamp(subZeroPct, 0, 80)
            };
            _displayProfiles.Add(profile);
            SaveToDisk();
            return Task.FromResult(Result<DisplayProfile>.Success(profile));
        }
    }

    public Task<Result<DisplayProfile>> DuplicateDisplayProfileAsync(string profileId)
    {
        lock (_lock)
        {
            var source = _displayProfiles.FirstOrDefault(p => p.Id == profileId);
            if (source == null) return Task.FromResult(Result<DisplayProfile>.Failure("Source profile not found"));

            var copy = source.Clone();
            copy.IsCustom = true;
            _displayProfiles.Add(copy);
            SaveToDisk();
            return Task.FromResult(Result<DisplayProfile>.Success(copy));
        }
    }

    public Task<Result> RenameDisplayProfileAsync(string profileId, string newName)
    {
        lock (_lock)
        {
            var profile = _displayProfiles.FirstOrDefault(p => p.Id == profileId);
            if (profile == null) return Task.FromResult(Result.Failure("Profile not found"));

            profile.Name = string.IsNullOrWhiteSpace(newName) ? "Unnamed Profile" : newName.Trim();
            SaveToDisk();
            return Task.FromResult(Result.Success());
        }
    }

    public Task<Result<AudioProfile>> CreateCustomAudioProfileAsync(string name, double[] bandGains, double bassGain, double trebleGain, string category = "Custom")
    {
        lock (_lock)
        {
            var profile = new AudioProfile
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = string.IsNullOrWhiteSpace(name) ? "Custom Audio Preset" : name.Trim(),
                Category = string.IsNullOrWhiteSpace(category) ? "Custom" : category,
                IsCustom = true,
                BandGains = (double[])bandGains.Clone(),
                BassGainDb = bassGain,
                TrebleGainDb = trebleGain,
                IsLoudnessNormalizationEnabled = true
            };
            _audioProfiles.Add(profile);
            SaveToDisk();
            return Task.FromResult(Result<AudioProfile>.Success(profile));
        }
    }

    public Task<Result<AudioProfile>> DuplicateAudioProfileAsync(string profileId)
    {
        lock (_lock)
        {
            var source = _audioProfiles.FirstOrDefault(p => p.Id == profileId);
            if (source == null) return Task.FromResult(Result<AudioProfile>.Failure("Source profile not found"));

            var copy = source.Clone();
            copy.IsCustom = true;
            _audioProfiles.Add(copy);
            SaveToDisk();
            return Task.FromResult(Result<AudioProfile>.Success(copy));
        }
    }

    public Task<Result> RenameAudioProfileAsync(string profileId, string newName)
    {
        lock (_lock)
        {
            var profile = _audioProfiles.FirstOrDefault(p => p.Id == profileId);
            if (profile == null) return Task.FromResult(Result.Failure("Profile not found"));

            profile.Name = string.IsNullOrWhiteSpace(newName) ? "Unnamed Audio Preset" : newName.Trim();
            SaveToDisk();
            return Task.FromResult(Result.Success());
        }
    }

    public Task<Result> RestoreDefaultProfilesAsync()
    {
        lock (_lock)
        {
            InitializeDefaults();
            SaveToDisk();
            return Task.FromResult(Result.Success());
        }
    }

    // =========================================================================
    // PER-MONITOR HARDWARE PREFERENCES
    // =========================================================================

    public MonitorPreference? GetMonitorPreference(string monitorKey)
    {
        lock (_lock)
        {
            return _monitorPreferences.TryGetValue(monitorKey, out var pref) ? pref : null;
        }
    }

    public Task<Result> SaveMonitorPreferenceAsync(MonitorPreference preference)
    {
        lock (_lock)
        {
            preference.LastConnected = DateTime.Now;
            _monitorPreferences[preference.MonitorKey] = preference;
            SaveToDisk();
            return Task.FromResult(Result.Success());
        }
    }

    // =========================================================================
    // AUDIO PRESET IMPORT / EXPORT
    // =========================================================================

    public Task<Result<string>> ExportAudioPresetAsync(string profileId, string filePath)
    {
        lock (_lock)
        {
            var profile = _audioProfiles.FirstOrDefault(p => p.Id == profileId);
            if (profile == null) return Task.FromResult(Result<string>.Failure("Audio preset not found."));

            try
            {
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
                return Task.FromResult(Result<string>.Success(filePath));
            }
            catch (Exception ex)
            {
                return Task.FromResult(Result<string>.Failure($"Export failed: {ex.Message}"));
            }
        }
    }

    public Task<Result<AudioProfile>> ImportAudioPresetAsync(string filePath)
    {
        if (!File.Exists(filePath)) return Task.FromResult(Result<AudioProfile>.Failure("File not found."));

        try
        {
            var json = File.ReadAllText(filePath);
            var profile = JsonSerializer.Deserialize<AudioProfile>(json);
            if (profile == null) return Task.FromResult(Result<AudioProfile>.Failure("Invalid preset file format."));

            lock (_lock)
            {
                profile.Id = Guid.NewGuid().ToString("N");
                profile.IsCustom = true;
                if (!profile.Name.Contains("(Imported)")) profile.Name = $"{profile.Name} (Imported)";
                _audioProfiles.Add(profile);
                SaveToDisk();
            }

            return Task.FromResult(Result<AudioProfile>.Success(profile));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<AudioProfile>.Failure($"Import failed: {ex.Message}"));
        }
    }

    // =========================================================================
    // DISPLAY PROFILE IMPORT / EXPORT (.prism-disp)
    // =========================================================================

    public Task<Result<string>> ExportDisplayProfileAsync(string profileId, string filePath)
    {
        lock (_lock)
        {
            var profile = _displayProfiles.FirstOrDefault(p => p.Id == profileId);
            if (profile == null) return Task.FromResult(Result<string>.Failure("Display profile not found."));

            try
            {
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
                return Task.FromResult(Result<string>.Success(filePath));
            }
            catch (Exception ex)
            {
                return Task.FromResult(Result<string>.Failure($"Export failed: {ex.Message}"));
            }
        }
    }

    public Task<Result<DisplayProfile>> ImportDisplayProfileAsync(string filePath)
    {
        if (!File.Exists(filePath)) return Task.FromResult(Result<DisplayProfile>.Failure("File not found."));

        try
        {
            var json = File.ReadAllText(filePath);
            var profile = JsonSerializer.Deserialize<DisplayProfile>(json);
            if (profile == null) return Task.FromResult(Result<DisplayProfile>.Failure("Invalid display profile format."));

            lock (_lock)
            {
                profile.Id = Guid.NewGuid().ToString("N");
                profile.IsCustom = true;
                profile.IsBuiltIn = false;
                profile.CreatedAt = DateTime.UtcNow;
                profile.UpdatedAt = DateTime.UtcNow;
                if (!profile.Name.Contains("(Imported)")) profile.Name = $"{profile.Name} (Imported)";
                _displayProfiles.Add(profile);
                SaveToDisk();
            }

            return Task.FromResult(Result<DisplayProfile>.Success(profile));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<DisplayProfile>.Failure($"Import failed: {ex.Message}"));
        }
    }

    // =========================================================================
    // FULL BACKUP & RESTORE / MODULE RESET
    // =========================================================================

    public class PrismFullBackup
    {
        public string Version { get; set; } = "2.1.0";
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public List<DisplayProfile> DisplayProfiles { get; set; } = new();
        public List<AudioProfile> AudioProfiles { get; set; } = new();
        public List<EnvironmentPreset> Environments { get; set; } = new();
        public AppSettings Settings { get; set; } = new();
        public List<MonitorPreference> MonitorPreferences { get; set; } = new();
    }

    public Task<Result<string>> CreateFullBackupAsync(string destinationPath)
    {
        lock (_lock)
        {
            try
            {
                var dir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var backup = new PrismFullBackup
                {
                    DisplayProfiles = _displayProfiles.ToList(),
                    AudioProfiles = _audioProfiles.ToList(),
                    Environments = _environments.ToList(),
                    Settings = _settings,
                    MonitorPreferences = _monitorPreferences.Values.ToList()
                };

                var json = JsonSerializer.Serialize(backup, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(destinationPath, json);
                return Task.FromResult(Result<string>.Success(destinationPath));
            }
            catch (Exception ex)
            {
                return Task.FromResult(Result<string>.Failure($"Backup creation failed: {ex.Message}"));
            }
        }
    }

    public Task<Result> RestoreFullBackupAsync(string sourcePath)
    {
        if (!File.Exists(sourcePath)) return Task.FromResult(Result.Failure("Backup file not found."));

        try
        {
            var json = File.ReadAllText(sourcePath);
            var backup = JsonSerializer.Deserialize<PrismFullBackup>(json);
            if (backup == null) return Task.FromResult(Result.Failure("Invalid backup file."));

            lock (_lock)
            {
                if (backup.DisplayProfiles?.Count > 0)
                {
                    _displayProfiles.Clear();
                    _displayProfiles.AddRange(backup.DisplayProfiles);
                }
                if (backup.AudioProfiles?.Count > 0)
                {
                    _audioProfiles.Clear();
                    _audioProfiles.AddRange(backup.AudioProfiles);
                }
                if (backup.Environments?.Count > 0)
                {
                    _environments.Clear();
                    _environments.AddRange(backup.Environments);
                }
                if (backup.Settings != null)
                {
                    _settings = backup.Settings;
                }
                if (backup.MonitorPreferences?.Count > 0)
                {
                    _monitorPreferences.Clear();
                    foreach (var m in backup.MonitorPreferences)
                    {
                        _monitorPreferences[m.MonitorKey] = m;
                    }
                }
                SaveToDisk();
            }

            return Task.FromResult(Result.Success());
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result.Failure($"Restore failed: {ex.Message}"));
        }
    }

    public Task<Result> ResetModuleAsync(string moduleName)
    {
        lock (_lock)
        {
            switch (moduleName.ToLowerInvariant())
            {
                case "display":
                    InitializeDisplayDefaults();
                    break;
                case "audio":
                    InitializeAudioDefaults();
                    break;
                case "environments":
                    InitializeEnvironmentDefaults();
                    break;
                case "all":
                    InitializeDefaults();
                    _settings = new AppSettings();
                    _monitorPreferences.Clear();
                    break;
                default:
                    return Task.FromResult(Result.Failure($"Unknown module '{moduleName}'."));
            }
            SaveToDisk();
            return Task.FromResult(Result.Success());
        }
    }

    // =========================================================================
    // UNIFIED ENVIRONMENT ENGINE
    // =========================================================================

    public IReadOnlyList<EnvironmentPreset> GetPresets() => _environments.AsReadOnly();
    public EnvironmentPreset? ActivePreset => _activePreset;

    public async Task<Result<EnvironmentApplicationResult>> ApplyEnvironmentAsync(
        string presetId,
        IDisplayService displayService,
        IAudioService audioService,
        IEqualizerApoBridge apoBridge)
    {
        EnvironmentPreset? target;
        lock (_lock)
        {
            target = _environments.FirstOrDefault(e => e.Id == presetId);
        }

        if (target == null)
        {
            return Result<EnvironmentApplicationResult>.Failure($"Environment preset '{presetId}' not found.");
        }

        bool displaySucceeded = false;
        string? displayError = null;

        bool audioSucceeded = false;
        string? audioError = null;

        // 1. Coordinate Display Subsystem
        try
        {
            var mon = displayService.ActiveMonitor;
            if (mon != null)
            {
                // Execute sequentially to respect DDC/CI I2C bus pacing (no concurrent writes)
                await displayService.SetBrightnessAsync(mon.Id, target.DisplaySettings.Brightness);
                await displayService.SetContrastAsync(mon.Id, target.DisplaySettings.Contrast);
                await displayService.SetRgbGainsAsync(mon.Id, target.DisplaySettings.RedGain, target.DisplaySettings.GreenGain, target.DisplaySettings.BlueGain);
                await displayService.SetSubZeroDimAsync(mon.Id, target.DisplaySettings.IsSubZeroDimEnabled, target.DisplaySettings.SubZeroDimPercent);

                displaySucceeded = true;
            }
            else
            {
                displayError = "No active physical monitor found.";
            }
        }
        catch (Exception ex)
        {
            displayError = ex.Message;
        }

        // 2. Coordinate Audio Subsystem
        try
        {
            if (!string.IsNullOrEmpty(target.TargetAudioDeviceId))
            {
                await audioService.SetActiveDeviceAsync(target.TargetAudioDeviceId);
            }

            var apoStatus = apoBridge.CheckStatus();
            if (apoStatus.IsInstalled)
            {
                var apoRes = await apoBridge.ApplyProfileAsync(target.AudioSettings);
                audioSucceeded = apoRes.IsSuccess;
                if (!audioSucceeded) audioError = apoRes.ErrorMessage;
            }
            else
            {
                audioSucceeded = false;
                audioError = "Equalizer APO is not installed on this system.";
            }
        }
        catch (Exception ex)
        {
            audioError = ex.Message;
        }

        // 3. Update Active State and Persist
        lock (_lock)
        {
            foreach (var e in _environments) e.IsActive = (e.Id == target.Id);
            _activePreset = target;
            _settings.ActiveEnvironmentId = target.Id;
            SaveToDisk();
        }

        // 4. Formulate Result
        EnvironmentApplicationResult appResult;
        if (displaySucceeded && audioSucceeded)
        {
            appResult = EnvironmentApplicationResult.Success(
                target.Name,
                $"{target.Name} applied: Display ({target.DisplaySettings.Brightness}%), Audio ({target.AudioSettings.Name}).");
        }
        else if (displaySucceeded || audioSucceeded)
        {
            appResult = EnvironmentApplicationResult.Partial(
                target.Name,
                displaySucceeded,
                audioSucceeded,
                displayError,
                audioError);
        }
        else
        {
            appResult = EnvironmentApplicationResult.Failure(
                target.Name,
                $"Failed to apply {target.Name}: {displayError ?? audioError}");
        }

        EnvironmentApplied?.Invoke(this, appResult);
        return Result<EnvironmentApplicationResult>.Success(appResult);
    }

    public Task<Result<EnvironmentPreset>> CreateCustomEnvironmentAsync(
        string name,
        string description,
        string iconKey,
        DisplayProfile display,
        AudioProfile audio,
        string? targetAudioDeviceId = null)
    {
        lock (_lock)
        {
            var env = new EnvironmentPreset
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = string.IsNullOrWhiteSpace(name) ? "Custom Environment" : name.Trim(),
                Description = string.IsNullOrWhiteSpace(description) ? "Custom user-defined studio environment" : description.Trim(),
                IconKey = string.IsNullOrWhiteSpace(iconKey) ? "SunIcon" : iconKey,
                IsCustom = true,
                DisplaySettings = display.Clone(),
                AudioSettings = audio.Clone(),
                TargetAudioDeviceId = targetAudioDeviceId
            };

            _environments.Add(env);
            SaveToDisk();
            return Task.FromResult(Result<EnvironmentPreset>.Success(env));
        }
    }

    public Task<Result<EnvironmentPreset>> DuplicateEnvironmentAsync(string presetId)
    {
        lock (_lock)
        {
            var source = _environments.FirstOrDefault(e => e.Id == presetId);
            if (source == null) return Task.FromResult(Result<EnvironmentPreset>.Failure("Source environment not found"));

            var copy = source.Clone();
            copy.IsCustom = true;
            _environments.Add(copy);
            SaveToDisk();
            return Task.FromResult(Result<EnvironmentPreset>.Success(copy));
        }
    }

    public Task<Result> RenameEnvironmentAsync(string presetId, string newName)
    {
        lock (_lock)
        {
            var env = _environments.FirstOrDefault(e => e.Id == presetId);
            if (env == null) return Task.FromResult(Result.Failure("Environment not found"));

            env.Name = string.IsNullOrWhiteSpace(newName) ? "Unnamed Environment" : newName.Trim();
            SaveToDisk();
            return Task.FromResult(Result.Success());
        }
    }

    public Task<Result> UpdateEnvironmentAsync(EnvironmentPreset preset)
    {
        lock (_lock)
        {
            var idx = _environments.FindIndex(e => e.Id == preset.Id);
            if (idx >= 0)
            {
                _environments[idx] = preset;
                SaveToDisk();
                return Task.FromResult(Result.Success());
            }
            return Task.FromResult(Result.Failure("Environment not found"));
        }
    }

    public Task<Result> DeleteEnvironmentAsync(string presetId)
    {
        lock (_lock)
        {
            var target = _environments.FirstOrDefault(e => e.Id == presetId);
            if (target == null) return Task.FromResult(Result.Failure("Environment not found"));
            if (!target.IsCustom) return Task.FromResult(Result.Failure("Cannot delete factory built-in environment"));

            _environments.Remove(target);
            if (_activePreset?.Id == presetId)
            {
                _activePreset = _environments.FirstOrDefault();
                if (_activePreset != null) _activePreset.IsActive = true;
            }
            SaveToDisk();
            return Task.FromResult(Result.Success());
        }
    }

    public Task<Result> RestoreDefaultEnvironmentsAsync()
    {
        lock (_lock)
        {
            InitializeDefaults();
            SaveToDisk();
            return Task.FromResult(Result.Success());
        }
    }

    public AppSettings GetSettings()
    {
        lock (_lock)
        {
            return _settings;
        }
    }

    public Task<Result> SaveSettingsAsync(AppSettings settings)
    {
        lock (_lock)
        {
            _settings = settings;
            SaveToDisk();
            return Task.FromResult(Result.Success());
        }
    }

    public EnvironmentSchedule GetSchedule()
    {
        lock (_lock)
        {
            return _settings.Schedule ??= new EnvironmentSchedule();
        }
    }

    public Task<Result> SaveScheduleAsync(EnvironmentSchedule schedule)
    {
        lock (_lock)
        {
            _settings.Schedule = schedule;
            SaveToDisk();
            return Task.FromResult(Result.Success());
        }
    }

    public void SetManualOverride(bool isOverridden)
    {
        lock (_lock)
        {
            _settings.Schedule.IsManualOverrideActive = isOverridden;
            SaveToDisk();
        }
    }

    private class ProfileContainer
    {
        public List<DisplayProfile>? DisplayProfiles { get; set; }
        public List<AudioProfile>? AudioProfiles { get; set; }
    }
}
