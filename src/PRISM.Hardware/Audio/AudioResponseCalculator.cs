using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using PRISM.Core.Models;

namespace PRISM.Hardware.Audio;

public static class AudioResponseCalculator
{
    private static readonly double[] GraphicFreqs = { 32, 64, 125, 250, 500, 1000, 2000, 4000, 8000, 16000 };
    private const double GraphicQ = 1.414; // 1-octave Q
    private const double MinFreq = 20.0;
    private const double MaxFreq = 20000.0;
    private const double MaxGainRangeDb = 15.0;

    public static double EvaluateGainDb(double freq, AudioProfile profile)
    {
        if (profile.IsBypassed) return 0.0;

        double totalGain = 0.0;

        // 1. 10 Graphic Bands Peaking Contribution
        if (profile.BandGains != null)
        {
            for (int i = 0; i < GraphicFreqs.Length; i++)
            {
                double f0 = GraphicFreqs[i];
                double gain = (i < profile.BandGains.Length) ? profile.BandGains[i] : 0.0;
                if (Math.Abs(gain) < 0.01) continue;

                double ratioDiff = (freq / f0) - (f0 / freq);
                double denom = 1.0 + (GraphicQ * GraphicQ * ratioDiff * ratioDiff);
                totalGain += gain / denom;
            }
        }

        // 2. Bass Enhancement Low Shelf (100 Hz)
        if (Math.Abs(profile.BassGainDb) > 0.01)
        {
            double r = freq / 100.0;
            totalGain += profile.BassGainDb / (1.0 + r * r);
        }

        // 3. Treble Air High Shelf (10,000 Hz)
        if (Math.Abs(profile.TrebleGainDb) > 0.01)
        {
            double r = freq / 10000.0;
            totalGain += profile.TrebleGainDb * (r * r) / (1.0 + r * r);
        }

        // 4. Parametric Filter Nodes
        if (profile.ParametricFilters != null)
        {
            foreach (var pf in profile.ParametricFilters.Where(f => f.IsEnabled))
            {
                double f0 = Math.Max(20.0, pf.Frequency);
                double q = Math.Max(0.1, pf.Q);

                switch (pf.Type)
                {
                    case ParametricFilterType.Peaking:
                        double diff = (freq / f0) - (f0 / freq);
                        totalGain += pf.GainDb / (1.0 + q * q * diff * diff);
                        break;
                    case ParametricFilterType.LowShelf:
                        double rL = freq / f0;
                        totalGain += pf.GainDb / (1.0 + rL * rL);
                        break;
                    case ParametricFilterType.HighShelf:
                        double rH = freq / f0;
                        totalGain += pf.GainDb * (rH * rH) / (1.0 + rH * rH);
                        break;
                    case ParametricFilterType.Notch:
                        double dN = Math.Abs((freq / f0) - (f0 / freq));
                        if (dN < (1.0 / q))
                        {
                            totalGain -= 24.0 * (1.0 - (dN * q));
                        }
                        break;
                    case ParametricFilterType.HighPass:
                        if (freq < f0)
                        {
                            double oct = Math.Log2(f0 / freq);
                            totalGain -= 12.0 * oct; // 12 dB/octave slope
                        }
                        break;
                    case ParametricFilterType.LowPass:
                        if (freq > f0)
                        {
                            double oct = Math.Log2(freq / f0);
                            totalGain -= 12.0 * oct;
                        }
                        break;
                }
            }
        }

        // 5. Preamp & Auto-Preamp Loudness Normalization
        double maxPositiveGain = 0.0;
        if (profile.BandGains != null)
        {
            foreach (var g in profile.BandGains) if (g > maxPositiveGain) maxPositiveGain = g;
        }
        if (profile.BassGainDb > maxPositiveGain) maxPositiveGain = profile.BassGainDb;
        if (profile.TrebleGainDb > maxPositiveGain) maxPositiveGain = profile.TrebleGainDb;

        double effectivePreamp = profile.PreampDb;
        if (profile.IsLoudnessNormalizationEnabled && maxPositiveGain > 0.0)
        {
            effectivePreamp -= maxPositiveGain;
        }

        totalGain += effectivePreamp;

        return totalGain;
    }

    public static PointCollection GenerateResponseCurvePoints(AudioProfile profile, double width, double height, int numSamples = 160)
    {
        var points = new PointCollection(numSamples);
        if (width <= 0 || height <= 0) return points;

        double logMin = Math.Log10(MinFreq);
        double logMax = Math.Log10(MaxFreq);
        double logSpan = logMax - logMin;
        double halfHeight = height / 2.0;

        for (int i = 0; i < numSamples; i++)
        {
            double t = (double)i / (numSamples - 1);
            double freq = Math.Pow(10.0, logMin + t * logSpan);
            double gainDb = EvaluateGainDb(freq, profile);

            double x = t * width;
            // +15 dB at y=0, 0 dB at y=halfHeight, -15 dB at y=height
            double y = halfHeight - (gainDb / MaxGainRangeDb) * halfHeight;
            y = Math.Clamp(y, 2.0, height - 2.0);

            points.Add(new Point(x, y));
        }

        return points;
    }

    public static Geometry GenerateAreaGeometry(PointCollection points, double width, double height)
    {
        var figure = new PathFigure
        {
            StartPoint = new Point(0, height / 2.0),
            IsClosed = true,
            IsFilled = true
        };

        if (points.Count > 0)
        {
            figure.Segments.Add(new LineSegment(points[0], false));
            for (int i = 1; i < points.Count; i++)
            {
                figure.Segments.Add(new LineSegment(points[i], true));
            }
            figure.Segments.Add(new LineSegment(new Point(width, height / 2.0), false));
        }

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }

    /// <summary>
    /// Generates a compact SVG Path string (e.g. "M 0,12 L 5,10 ...") representing the real frequency response curve.
    /// Perfectly sized for preset browsing cards and thumbnails.
    /// </summary>
    public static string GenerateMiniSvgPath(AudioProfile profile, double width = 80.0, double height = 24.0)
    {
        if (profile == null) return $"M 0,{height / 2.0:F1} L {width:F1},{height / 2.0:F1}";

        var sb = new System.Text.StringBuilder();
        int sampleCount = 20;
        double midY = height / 2.0;
        double maxGain = 12.0; // +/- 12 dB clamp for mini cards

        for (int i = 0; i < sampleCount; i++)
        {
            double t = (double)i / (sampleCount - 1);
            // Logarithmic frequency between 20 Hz and 20 kHz
            double freq = 20.0 * Math.Pow(20000.0 / 20.0, t);
            double gainDb = EvaluateGainDb(freq, profile);

            // Clamp and map gain to Y (gain of +12dB -> Y=2, 0dB -> Y=midY, -12dB -> Y=height-2)
            double clamped = Math.Clamp(gainDb, -maxGain, maxGain);
            double y = midY - (clamped / maxGain) * (midY - 2.0);
            double x = t * width;

            if (i == 0)
                sb.Append(System.Globalization.CultureInfo.InvariantCulture, $"M {x:F1},{y:F1}");
            else
                sb.Append(System.Globalization.CultureInfo.InvariantCulture, $" L {x:F1},{y:F1}");
        }

        return sb.ToString();
    }
}
