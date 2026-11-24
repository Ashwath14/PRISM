using System;

namespace PRISM.Core.Models;

public class EnvironmentPreset
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Daylight Focus";
    public string Description { get; set; } = "Balanced natural brightness and neutral sound";
    public string IconKey { get; set; } = "SunIcon";
    public string ImagePath { get; set; } = "/Assets/Environments/daylight_focus.jpg";
    public bool IsActive { get; set; }
    public bool IsCustom { get; set; }
    
    public DisplayProfile DisplaySettings { get; set; } = new();
    public AudioProfile AudioSettings { get; set; } = new();

    // Optional specific target audio device ID (null = current active device)
    public string? TargetAudioDeviceId { get; set; }

    public EnvironmentPreset Clone()
    {
        return new EnvironmentPreset
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = $"{Name} (Copy)",
            Description = Description,
            IconKey = IconKey,
            ImagePath = ImagePath,
            IsCustom = true,
            IsActive = false,
            TargetAudioDeviceId = TargetAudioDeviceId,
            DisplaySettings = DisplaySettings.Clone(),
            AudioSettings = AudioSettings.Clone()
        };
    }
}
