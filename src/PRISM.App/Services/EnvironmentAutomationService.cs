using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using PRISM.Core.Interfaces;
using PRISM.Core.Models;

namespace PRISM.App.Services;

public class EnvironmentAutomationService : IDisposable
{
    private readonly IEnvironmentService _environmentService;
    private readonly IDisplayService _displayService;
    private readonly IAudioService _audioService;
    private readonly IEqualizerApoBridge _apoBridge;

    private readonly System.Threading.Timer _timer;
    private bool _isDisposed;
    private string? _lastAutomatedTarget;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

    public event Action<string>? AutomatedEnvironmentSwitched;
    public event Action<string>? AutomationDeferred;

    public EnvironmentAutomationService(
        IEnvironmentService environmentService,
        IDisplayService displayService,
        IAudioService audioService,
        IEqualizerApoBridge apoBridge)
    {
        _environmentService = environmentService;
        _displayService = displayService;
        _audioService = audioService;
        _apoBridge = apoBridge;

        // Check every 30 seconds
        _timer = new System.Threading.Timer(OnTimerTick, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30));
    }

    private void OnTimerTick(object? state)
    {
        if (_isDisposed) return;

        try
        {
            var schedule = _environmentService.GetSchedule();
            if (!schedule.IsEnabled) return;

            // If manual override is active, respect user choice
            if (schedule.IsManualOverrideActive) return;

            var now = DateTime.Now.TimeOfDay;
            string targetName;

            if (schedule.DayStartTime < schedule.NightStartTime)
            {
                // Normal day window: e.g. 07:00 to 20:00
                bool isDay = now >= schedule.DayStartTime && now < schedule.NightStartTime;
                targetName = isDay ? schedule.DayEnvironmentName : schedule.NightEnvironmentName;
            }
            else
            {
                // Overnight window
                bool isDay = now >= schedule.DayStartTime || now < schedule.NightStartTime;
                targetName = isDay ? schedule.DayEnvironmentName : schedule.NightEnvironmentName;
            }

            var current = _environmentService.ActivePreset;
            if (current != null && current.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase))
            {
                // Already in target environment
                return;
            }

            // Check if user is in a fullscreen game / media presentation
            if (schedule.PauseDuringFullscreenMedia && IsUserInFullscreenApplication())
            {
                AutomationDeferred?.Invoke($"Scheduled switch to '{targetName}' deferred (Fullscreen application active).");
                return;
            }

            // Find target preset
            var preset = _environmentService.GetPresets()
                .FirstOrDefault(p => p.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase));

            if (preset != null && _lastAutomatedTarget != targetName)
            {
                _lastAutomatedTarget = targetName;
                _ = Task.Run(async () =>
                {
                    var result = await _environmentService.ApplyEnvironmentAsync(
                        preset.Id,
                        _displayService,
                        _audioService,
                        _apoBridge);

                    if (result.IsSuccess)
                    {
                        AutomatedEnvironmentSwitched?.Invoke(targetName);
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[EnvironmentAutomationService Error] {ex.Message}");
        }
    }

    private bool IsUserInFullscreenApplication()
    {
        try
        {
            IntPtr fgWindow = GetForegroundWindow();
            if (fgWindow == IntPtr.Zero) return false;

            var sb = new System.Text.StringBuilder(256);
            GetClassName(fgWindow, sb, 256);
            string cls = sb.ToString();

            // Ignore desktop and taskbar
            if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd") return false;

            if (GetWindowRect(fgWindow, out RECT rect))
            {
                int width = rect.Right - rect.Left;
                int height = rect.Bottom - rect.Top;

                var mon = _displayService.ActiveMonitor;
                int monW = mon?.Width ?? 1920;
                int monH = mon?.Height ?? 1080;

                return width >= monW && height >= monH;
            }
        }
        catch { }

        return false;
    }

    public void Dispose()
    {
        _isDisposed = true;
        _timer.Dispose();
    }
}
