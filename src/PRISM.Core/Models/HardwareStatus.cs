using System;

namespace PRISM.Core.Models;

public enum HardwareCommunicationState
{
    Connected,
    Reading,
    Applying,
    Success,
    Failed,
    Unsupported
}

public class HardwareStatus
{
    public HardwareCommunicationState State { get; set; } = HardwareCommunicationState.Connected;
    public string StatusText { get; set; } = "Hardware Ready (DDC/CI Active)";
    public long LastLatencyMs { get; set; } = 0;
    public int? LastVerifiedBrightness { get; set; }
    public int? LastVerifiedContrast { get; set; }
    public int? LastVerifiedRed { get; set; }
    public int? LastVerifiedGreen { get; set; }
    public int? LastVerifiedBlue { get; set; }
    public string? LastErrorMessage { get; set; }
    public DateTime LastOperationTime { get; set; } = DateTime.Now;
}
