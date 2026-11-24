using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using PRISM.Core.Interfaces;
using PRISM.Core.Models;

namespace PRISM.App.Services;

public class TrayIconService : IDisposable
{
    private readonly IDisplayService _displayService;
    private readonly IAudioService _audioService;
    private readonly IEnvironmentService _environmentService;
    private readonly IEqualizerApoBridge _apoBridge;

    private readonly Action _showMainWindowAction;
    private readonly Action _showFlyoutAction;
    private readonly Action _showSettingsAction;
    private readonly Action _exitAppAction;

    private NotifyIcon? _notifyIcon;
    private ContextMenuStrip? _contextMenu;
    private Icon? _generatedIcon;
    private bool _disposed;

    public TrayIconService(
        IDisplayService displayService,
        IAudioService audioService,
        IEnvironmentService environmentService,
        IEqualizerApoBridge apoBridge,
        Action showMainWindowAction,
        Action showFlyoutAction,
        Action showSettingsAction,
        Action exitAppAction)
    {
        _displayService = displayService;
        _audioService = audioService;
        _environmentService = environmentService;
        _apoBridge = apoBridge;

        _showMainWindowAction = showMainWindowAction;
        _showFlyoutAction = showFlyoutAction;
        _showSettingsAction = showSettingsAction;
        _exitAppAction = exitAppAction;

        InitializeTrayIcon();
    }

    private void InitializeTrayIcon()
    {
        try
        {
            _generatedIcon = CreatePrismIcon();
            _contextMenu = new ContextMenuStrip();
            _contextMenu.Opening += (s, e) => RebuildContextMenu();

            _notifyIcon = new NotifyIcon
            {
                Icon = _generatedIcon,
                Text = "PRISM — Display & Audio Studio",
                Visible = true,
                ContextMenuStrip = _contextMenu
            };

            _notifyIcon.DoubleClick += (s, e) => _showMainWindowAction();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to initialize system tray icon: {ex.Message}");
        }
    }

    private Icon CreatePrismIcon()
    {
        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string icoPath = System.IO.Path.Combine(baseDir, "Assets", "PRISM.ico");
            if (System.IO.File.Exists(icoPath))
            {
                return new Icon(icoPath, 32, 32);
            }
        }
        catch { }

        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent); // Clean transparent background

            // Crisp 32x32 Pixel-Snapped Refractive 'P' Geometry (Phase 2 Brand Identity)
            using var brush = new SolidBrush(Color.FromArgb(0, 199, 255)); // Electric Cyan #00C7FF
            using var path = new GraphicsPath();

            path.AddPolygon(new PointF[]
            {
                new PointF(6f, 4f),
                new PointF(18f, 4f),
                new PointF(24f, 6.5f),
                new PointF(26f, 13f),
                new PointF(24f, 19.5f),
                new PointF(18f, 22f),
                new PointF(12f, 22f),
                new PointF(12f, 28f),
                new PointF(6f, 28f)
            });

            g.FillPath(brush, path);

            // Refractive Specular Core
            using var coreBrush = new SolidBrush(Color.FromArgb(220, 255, 255, 255));
            g.FillPolygon(coreBrush, new PointF[]
            {
                new PointF(12f, 9f),
                new PointF(17.5f, 9f),
                new PointF(20.5f, 11f),
                new PointF(12f, 15.5f)
            });
        }

        IntPtr hIcon = bitmap.GetHicon();
        return Icon.FromHandle(hIcon);
    }

    private void RebuildContextMenu()
    {
        if (_contextMenu == null) return;
        _contextMenu.Items.Clear();

        // 1. Open PRISM (Default)
        var openItem = new ToolStripMenuItem("Open PRISM", null, (s, e) => _showMainWindowAction())
        {
            Font = new Font(_contextMenu.Font, System.Drawing.FontStyle.Bold)
        };
        _contextMenu.Items.Add(openItem);

        // 2. Quick Controls (Win + Alt + P)
        _contextMenu.Items.Add(new ToolStripMenuItem("Quick Controls (Win + Alt + P)", null, (s, e) => _showFlyoutAction()));
        _contextMenu.Items.Add(new ToolStripSeparator());

        // 3. Environments Submenu
        var envMenu = new ToolStripMenuItem("Environments");
        var activePreset = _environmentService.ActivePreset;
        foreach (var env in _environmentService.GetPresets())
        {
            var item = new ToolStripMenuItem(env.Name, null, async (s, e) =>
            {
                await _environmentService.ApplyEnvironmentAsync(env.Id, _displayService, _audioService, _apoBridge);
            })
            {
                Checked = (activePreset?.Id == env.Id)
            };
            envMenu.DropDownItems.Add(item);
        }
        _contextMenu.Items.Add(envMenu);

        // 4. Display Brightness Submenu
        var mon = _displayService.ActiveMonitor;
        if (mon != null)
        {
            var brightMenu = new ToolStripMenuItem($"Brightness ({mon.CurrentBrightness}%)");
            int[] levels = { 100, 75, 50, 25, 10 };
            foreach (var lvl in levels)
            {
                var lvlItem = new ToolStripMenuItem($"{lvl}%", null, async (s, e) =>
                {
                    await _displayService.SetBrightnessAsync(mon.Id, lvl);
                })
                {
                    Checked = (mon.CurrentBrightness == lvl)
                };
                brightMenu.DropDownItems.Add(lvlItem);
            }
            _contextMenu.Items.Add(brightMenu);
        }

        // 5. Audio Output Device Submenu
        var devMenu = new ToolStripMenuItem("Audio Output Device");
        var activeDev = _audioService.ActiveDevice;
        foreach (var dev in _audioService.GetConnectedDevices())
        {
            var devItem = new ToolStripMenuItem(dev.Name, null, async (s, e) =>
            {
                await _audioService.SetActiveDeviceAsync(dev.Id);
            })
            {
                Checked = (activeDev?.Id == dev.Id)
            };
            devMenu.DropDownItems.Add(devItem);
        }
        _contextMenu.Items.Add(devMenu);

        _contextMenu.Items.Add(new ToolStripSeparator());

        // 6. Settings
        _contextMenu.Items.Add(new ToolStripMenuItem("Settings", null, (s, e) => _showSettingsAction()));

        // 7. Exit PRISM
        _contextMenu.Items.Add(new ToolStripMenuItem("Exit PRISM", null, (s, e) => _exitAppAction()));
    }

    public void ShowNotification(string title, string message)
    {
        _notifyIcon?.ShowBalloonTip(3000, title, message, ToolTipIcon.Info);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }
            if (_contextMenu != null)
            {
                _contextMenu.Dispose();
                _contextMenu = null;
            }
            if (_generatedIcon != null)
            {
                _generatedIcon.Dispose();
                _generatedIcon = null;
            }
        }
    }
}
