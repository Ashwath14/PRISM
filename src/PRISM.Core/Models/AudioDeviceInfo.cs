namespace PRISM.Core.Models;

public enum AudioDeviceType
{
    Headphones,
    Speakers,
    DigitalOutput,
    Other
}

public class AudioDeviceInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = "Default Audio Device";
    public bool IsDefault { get; set; }
    public bool IsDefaultCommunications { get; set; }
    public int VolumePercent { get; set; } = 50;
    public bool IsMuted { get; set; }
    public AudioDeviceType DeviceType { get; set; } = AudioDeviceType.Speakers;
    public string AdapterName { get; set; } = string.Empty;
}
