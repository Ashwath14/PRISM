using System;
using System.Collections.Generic;

namespace PRISM.Core.Models;

public class AudioProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Flat (Default)";
    public string Category { get; set; } = "Audiophile / Music";
    public string Description { get; set; } = "Uncolored bit-exact reference curve with 0 dB across all bands.";
    public string IntendedExperience { get; set; } = "Neutral studio monitoring, audio analysis, and bit-exact listening.";
    public bool IsCustom { get; set; }
    
    // 10-Band EQ gains in dB (-12.0 to +12.0 dB)
    // Frequencies: 32, 64, 125, 250, 500, 1k, 2k, 4k, 8k, 16k Hz
    public double[] BandGains { get; set; } = new double[10];

    // Preamp in dB (-20.0 to +10.0 dB)
    public double PreampDb { get; set; } = 0.0;
    
    // Auto-preamp loudness normalization (safeguard against clipping)
    public bool IsLoudnessNormalizationEnabled { get; set; } = true;

    // Acoustic enhancement shelves
    public double BassGainDb { get; set; } = 0.0;
    public double TrebleGainDb { get; set; } = 0.0;

    // Parametric filter nodes
    public List<ParametricFilter> ParametricFilters { get; set; } = new();

    // Dynamics / Compressor
    public bool IsCompressorEnabled { get; set; }
    public double CompressorThresholdDb { get; set; } = -12.0;
    public double CompressorRatio { get; set; } = 3.0;

    // Stereo widening
    public bool IsStereoWideningEnabled { get; set; }
    public int StereoWidthPercent { get; set; } = 100;

    // Master DSP Bypass (A/B testing)
    public bool IsBypassed { get; set; }

    // Optional target device restriction (null = all endpoints)
    public string? TargetDeviceName { get; set; }

    public AudioProfile Clone()
    {
        var clone = new AudioProfile
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = $"{Name} (Copy)",
            Category = Category,
            Description = Description,
            IntendedExperience = IntendedExperience,
            IsCustom = true,
            BandGains = (double[])BandGains.Clone(),
            PreampDb = PreampDb,
            IsLoudnessNormalizationEnabled = IsLoudnessNormalizationEnabled,
            BassGainDb = BassGainDb,
            TrebleGainDb = TrebleGainDb,
            IsCompressorEnabled = IsCompressorEnabled,
            CompressorThresholdDb = CompressorThresholdDb,
            CompressorRatio = CompressorRatio,
            IsStereoWideningEnabled = IsStereoWideningEnabled,
            StereoWidthPercent = StereoWidthPercent,
            IsBypassed = IsBypassed,
            TargetDeviceName = TargetDeviceName
        };

        foreach (var f in ParametricFilters)
        {
            clone.ParametricFilters.Add(new ParametricFilter
            {
                Id = Guid.NewGuid().ToString("N"),
                IsEnabled = f.IsEnabled,
                Type = f.Type,
                Frequency = f.Frequency,
                GainDb = f.GainDb,
                Q = f.Q
            });
        }

        return clone;
    }
}
