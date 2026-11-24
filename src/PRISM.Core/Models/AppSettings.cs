namespace PRISM.Core.Models;

public class AppSettings
{
    public bool StartWithWindows { get; set; } = false;
    public bool MinimizeToTray { get; set; } = true;
    public string GlobalShortcut { get; set; } = "Win + Alt + P";
    public string ActiveEnvironmentId { get; set; } = "env_day";
    public bool AutoCloseFlyoutOnDeactivate { get; set; } = true;
    public bool AutoRestoreMonitorProfiles { get; set; } = true;
    public bool IsCompactQuickControl { get; set; } = false;
    public EnvironmentSchedule Schedule { get; set; } = new();
}
