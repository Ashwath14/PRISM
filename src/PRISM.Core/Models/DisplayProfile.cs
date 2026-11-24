using System;
using System.Text.Json.Serialization;

namespace PRISM.Core.Models;

public enum DisplayCapabilityStatus
{
    Supported,
    Unsupported,
    Unavailable,
    NotConfigured
}

public class DisplayCapabilityInfo
{
    public DisplayCapabilityStatus Status { get; set; } = DisplayCapabilityStatus.Supported;
    public string DisplayText { get; set; } = "Hardware Supported";
    public string Reason { get; set; } = "";

    public static DisplayCapabilityInfo Supported(string text = "Hardware Supported") => 
        new() { Status = DisplayCapabilityStatus.Supported, DisplayText = text };

    public static DisplayCapabilityInfo Unsupported(string reason, string text = "Not supported by monitor") => 
        new() { Status = DisplayCapabilityStatus.Unsupported, Reason = reason, DisplayText = text };

    public static DisplayCapabilityInfo Unavailable(string reason, string text = "Temporarily Unavailable") => 
        new() { Status = DisplayCapabilityStatus.Unavailable, Reason = reason, DisplayText = text };

    public static DisplayCapabilityInfo NotConfigured(string text = "Native / Not exposed by monitor") => 
        new() { Status = DisplayCapabilityStatus.NotConfigured, DisplayText = text };
}

public class DisplayProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Daylight Focus";
    public string Description { get; set; } = "Balanced daytime productivity with natural clarity and neutral color";
    public string Category { get; set; } = "Productivity";

    // Primary DDC/CI hardware settings (Normalized 0-100)
    public int Brightness { get; set; } = 55;
    public int Contrast { get; set; } = 50;
    public int Sharpness { get; set; } = 50; // Native / Default
    public int RedGain { get; set; } = 50;
    public int GreenGain { get; set; } = 50;
    public int BlueGain { get; set; } = 50;
    public int Saturation { get; set; } = 50; // Unexposed on Acer ED270
    public double Gamma { get; set; } = 2.2; // Native panel 2.2 curve
    public string ColorTemperature { get; set; } = "Neutral (6500K)";

    // Software dimming layer below physical backlight 0%
    public bool SoftwareDimmingEnabled { get; set; } = false;
    public int SoftwareDimmingLevel { get; set; } = 0;

    // Metadata & Classification
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsBuiltIn { get; set; } = false;
    public bool IsCustom { get; set; } = false;

    // Backward-compatibility aliases for existing services and tests
    [JsonIgnore]
    public string ProfileId => Id;

    [JsonIgnore]
    public bool IsSubZeroDimEnabled
    {
        get => SoftwareDimmingEnabled;
        set => SoftwareDimmingEnabled = value;
    }

    [JsonIgnore]
    public int SubZeroDimPercent
    {
        get => SoftwareDimmingLevel;
        set => SoftwareDimmingLevel = value;
    }

    // Explicit hardware capability information (Honest reporting per Phase 2 & Phase 4)
    [JsonIgnore]
    public DisplayCapabilityInfo BrightnessCapability => 
        DisplayCapabilityInfo.Supported("VCP 0x10 (Continuous 0-100%)");

    [JsonIgnore]
    public DisplayCapabilityInfo ContrastCapability => 
        DisplayCapabilityInfo.Supported("VCP 0x12 (Continuous 0-100%)");

    [JsonIgnore]
    public DisplayCapabilityInfo RgbGainCapability => 
        DisplayCapabilityInfo.Supported("VCP 0x16, 0x18, 0x1A (Continuous 0-100%)");

    [JsonIgnore]
    public DisplayCapabilityInfo SharpnessCapability => 
        DisplayCapabilityInfo.NotConfigured("Native / Not exposed by monitor");

    [JsonIgnore]
    public DisplayCapabilityInfo SaturationCapability => 
        DisplayCapabilityInfo.Unsupported("Acer ED270 X0 scaler does not expose VCP 0x8A", "Not supported by monitor");

    [JsonIgnore]
    public DisplayCapabilityInfo GammaCapability => 
        DisplayCapabilityInfo.NotConfigured("Native panel curve (System default 2.2)");

    [JsonIgnore]
    public DisplayCapabilityInfo ColorTemperatureCapability => 
        DisplayCapabilityInfo.Supported("VCP 0x14 Preset & RGB Balance");

    [JsonIgnore]
    public DisplayCapabilityInfo SoftwareDimmingCapability => 
        DisplayCapabilityInfo.Supported("DirectComposition Layered Window Engine");

    public DisplayProfile Clone()
    {
        return new DisplayProfile
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = $"{Name} (Copy)",
            Description = Description,
            Category = Category,
            Brightness = Brightness,
            Contrast = Contrast,
            Sharpness = Sharpness,
            RedGain = RedGain,
            GreenGain = GreenGain,
            BlueGain = BlueGain,
            Saturation = Saturation,
            Gamma = Gamma,
            ColorTemperature = ColorTemperature,
            SoftwareDimmingEnabled = SoftwareDimmingEnabled,
            SoftwareDimmingLevel = SoftwareDimmingLevel,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsBuiltIn = false,
            IsCustom = true
        };
    }
}
