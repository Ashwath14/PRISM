using System;

namespace PRISM.Core.Models;

public class EnvironmentSchedule
{
    public bool IsEnabled { get; set; } = false;
    public string DayEnvironmentName { get; set; } = "Daylight Focus";
    public string NightEnvironmentName { get; set; } = "Late Night";
    public TimeSpan DayStartTime { get; set; } = new TimeSpan(7, 0, 0);   // 07:00 AM
    public TimeSpan NightStartTime { get; set; } = new TimeSpan(20, 0, 0); // 08:00 PM
    public bool PauseDuringFullscreenMedia { get; set; } = true;
    public bool IsManualOverrideActive { get; set; } = false;
    public string? LastAutomatedEnvironmentName { get; set; }
}
