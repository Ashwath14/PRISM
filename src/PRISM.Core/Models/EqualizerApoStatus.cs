namespace PRISM.Core.Models;

public class EqualizerApoStatus
{
    public bool IsInstalled { get; set; }
    public string? InstallPath { get; set; }
    public string? Version { get; set; }
    public bool IsConfiguredForActiveDevice { get; set; }
    public string ConfigFilePath { get; set; } = string.Empty;
    public string StatusMessage { get; set; } = "Equalizer APO detection pending";
    public string GuidanceTip { get; set; } = string.Empty;
}
