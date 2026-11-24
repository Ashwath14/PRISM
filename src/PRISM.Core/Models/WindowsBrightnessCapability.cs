using System;

namespace PRISM.Core.Models;

public enum WindowsBrightnessSupportType
{
    SupportedInternalWmi,
    UnsupportedExternalDdcCi,
    NoDisplaysDetected,
    Unavailable
}

public class WindowsBrightnessCapability
{
    public bool IsQuickSettingsSyncSupported { get; set; }
    public WindowsBrightnessSupportType SupportType { get; set; }
    public string DisplayName { get; set; } = "";
    public string DeviceInstanceId { get; set; } = "";
    public bool IsInternalDisplay { get; set; }
    public int CurrentWindowsBrightness { get; set; } = -1;
    public string TechnicalLimitationExplanation { get; set; } = "";
    public string RecommendedInterface { get; set; } = "DDC/CI VCP 0x10";
}
