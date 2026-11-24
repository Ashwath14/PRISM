using System;

namespace PRISM.Core.Models;

public class MonitorPreference
{
    public string MonitorKey { get; set; } = string.Empty;
    public string MonitorName { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string? LastAppliedProfileName { get; set; }
    
    public int SavedBrightness { get; set; } = 44;
    public int SavedContrast { get; set; } = 50;
    public int SavedRedGain { get; set; } = 50;
    public int SavedGreenGain { get; set; } = 50;
    public int SavedBlueGain { get; set; } = 50;

    public bool SavedSubZeroEnabled { get; set; } = false;
    public int SavedSubZeroPercent { get; set; } = 0;

    public DateTime LastConnected { get; set; } = DateTime.Now;
}
