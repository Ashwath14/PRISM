using System;

namespace PRISM.Core.Models;

public class DisplayHdrInfo
{
    public bool IsHdrSupported { get; set; } = false;
    public bool IsHdrEnabled { get; set; } = false;
    public string ColorEncoding { get; set; } = "RGB";
    public int BitsPerChannel { get; set; } = 8;
    public double MaxLuminanceNits { get; set; } = 250.0;
    public double SdrWhiteLevelNits { get; set; } = 200.0;
    public string IccProfilePath { get; set; } = "System Default sRGB (sRGB IEC61966-2.1)";
    public string DolbyVisionStatus { get; set; } = "Unsupported on 8-bit SDR hardware";
    public string SummaryText { get; set; } = "SDR 8-bit Standard Dynamic Range";
}
