namespace PRISM.Core.Models;

public class VcpFeatureReport
{
    public bool IsDdcCiSupported { get; set; }
    public bool SupportsBrightness { get; set; }
    public bool SupportsContrast { get; set; }
    public bool SupportsRgbGain { get; set; }
    public bool SupportsInputSelect { get; set; }
    public bool SupportsPowerMode { get; set; }
    public string? RawCapabilitiesString { get; set; }
    public string DiagnosticNotes { get; set; } = string.Empty;
}
