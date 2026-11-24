using System;

namespace PRISM.Core.Models;

public class SpatialAudioInfo
{
    public bool IsSupported { get; set; } = true;
    public bool IsActive { get; set; } = false;
    public string ActiveFormatName { get; set; } = "Off (Stereo / Direct)";
    public bool IsDolbyAccessInstalled { get; set; } = false;
    public bool IsDtsInstalled { get; set; } = false;
    public string StatusMessage { get; set; } = "Windows Spatial Sound ready for headphones and multi-channel endpoints.";
}
