using System;

namespace PRISM.Core.Models;

public class DisplayModeInfo : IEquatable<DisplayModeInfo>
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int RefreshRate { get; set; }
    public int BitsPerPixel { get; set; } = 32;
    public bool IsNative { get; set; }
    public bool IsActive { get; set; }

    public string ResolutionText => $"{Width} × {Height}";
    public string RefreshRateText => $"{RefreshRate} Hz";
    public string AspectRatioText => CalculateAspectRatio(Width, Height);

    public string DisplayName =>
        $"{Width} × {Height} @ {RefreshRate} Hz" + (IsNative ? " (Native)" : "") + (IsActive ? " [Active]" : "");

    private static string CalculateAspectRatio(int w, int h)
    {
        if (w <= 0 || h <= 0) return "Unknown";
        int gcd = GCD(w, h);
        int rw = w / gcd;
        int rh = h / gcd;

        // Common aspect ratio mappings
        if (rw == 16 && rh == 9) return "16:9 Widescreen";
        if (rw == 16 && rh == 10) return "16:10";
        if (rw == 4 && rh == 3) return "4:3 Standard";
        if (rw == 21 && rh == 9) return "21:9 Ultrawide";
        if (rw == 32 && rh == 9) return "32:9 Super Ultrawide";

        return $"{rw}:{rh}";
    }

    private static int GCD(int a, int b)
    {
        while (b != 0)
        {
            int temp = b;
            b = a % b;
            a = temp;
        }
        return a;
    }

    public bool Equals(DisplayModeInfo? other)
    {
        if (other is null) return false;
        return Width == other.Width && Height == other.Height && RefreshRate == other.RefreshRate;
    }

    public override bool Equals(object? obj) => Equals(obj as DisplayModeInfo);

    public override int GetHashCode() => HashCode.Combine(Width, Height, RefreshRate);

    public override string ToString() => DisplayName;
}
