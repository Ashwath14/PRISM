using System;
using System.Collections.Generic;

namespace PRISM.Core.Models;

public enum HealthStatus
{
    Healthy,
    Limited,
    Unavailable,
    Error,
    NotTested
}

public class HealthCheckItem
{
    public string Component { get; set; } = "";
    public string Category { get; set; } = ""; // "Display", "Audio", "DSP", "System"
    public HealthStatus Status { get; set; } = HealthStatus.NotTested;
    public string Details { get; set; } = "";
    public string TechnicalMetrics { get; set; } = "";

    public string StatusText => Status switch
    {
        HealthStatus.Healthy => "HEALTHY",
        HealthStatus.Limited => "LIMITED",
        HealthStatus.Unavailable => "UNAVAILABLE",
        HealthStatus.Error => "ERROR",
        _ => "NOT TESTED"
    };

    public string StatusBadgeBackground => Status switch
    {
        HealthStatus.Healthy => "#2063C99A",
        HealthStatus.Limited => "#26D6A76A",
        HealthStatus.Unavailable => "#1E8993A0",
        HealthStatus.Error => "#26E17D85",
        _ => "#15FFFFFF"
    };

    public string StatusBadgeForeground => Status switch
    {
        HealthStatus.Healthy => "#63C99A",
        HealthStatus.Limited => "#D6A76A",
        HealthStatus.Unavailable => "#8993A0",
        HealthStatus.Error => "#E17D85",
        _ => "#B9C0CA"
    };

    public string StatusBadgeBorder => Status switch
    {
        HealthStatus.Healthy => "#4D63C99A",
        HealthStatus.Limited => "#4DD6A76A",
        HealthStatus.Unavailable => "#338993A0",
        HealthStatus.Error => "#4DE17D85",
        _ => "#26FFFFFF"
    };
}

public class HealthCheckReport
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string OverallStatus { get; set; } = "Healthy";
    public List<HealthCheckItem> Items { get; set; } = new();
    public string FormattedSummaryText { get; set; } = "";
}
