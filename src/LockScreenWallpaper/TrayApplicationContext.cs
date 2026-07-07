using Microsoft.Win32;
using System.Diagnostics;

namespace LockScreenWallpaper;

/// <summary>
/// Hosts the tray icon, settings menu, and the SessionSwitch hook that shows/hides
/// the overlay windows when the workstation locks and unlocks.
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "LockScreenWallpaper";

    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LockScreenWallpaper", "events.log");

    private readonly Stopwatch _sinceLastEvent = Stopwatch.StartNew();
    private readonly NotifyIcon _trayIcon;
    private readonly OverlayManager _overlayManager = new();
    private readonly System.Windows.Forms.Timer _previewTimer;
    private AppSettings _settings;

    public TrayApplicationContext()
    {
        _settings = AppSettings.Load();

        _previewTimer = new System.Windows.Forms.Timer { Interval = 5000 };
        _previewTimer.Tick += (_, _) =>
        {
            _previewTimer.Stop();
            _overlayManager.HideAll();
        };

        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Lock Screen Wallpaper",
            Visible = true,
            ContextMenuStrip = BuildMenu(),
        };
        _trayIcon.DoubleClick += (_, _) => PreviewOverlays();

        SystemEvents.SessionSwitch += OnSessionSwitch;
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        var setDefaultItem = new ToolStripMenuItem("Set default wallpaper...");
        setDefaultItem.Click += (_, _) => SetDefaultImage();
        menu.Items.Add(setDefaultItem);

        var perMonitorMenu = new ToolStripMenuItem("Set wallpaper for monitor");
        menu.Items.Add(perMonitorMenu);
        menu.Opening += (_, _) => RebuildPerMonitorMenu(perMonitorMenu);
        RebuildPerMonitorMenu(perMonitorMenu);

        menu.Items.Add(new ToolStripSeparator());

        var previewItem = new ToolStripMenuItem("Preview overlay (5s)");
        previewItem.Click += (_, _) => PreviewOverlays();
        menu.Items.Add(previewItem);

        var logItem = new ToolStripMenuItem("Open event log");
        logItem.Click += (_, _) => OpenEventLog();
        menu.Items.Add(logItem);

        menu.Items.Add(new ToolStripSeparator());

        var startupItem = new ToolStripMenuItem("Start with Windows")
        {
            CheckOnClick = true,
            Checked = IsStartupEnabled(),
        };
        startupItem.Click += (_, _) => SetStartupEnabled(startupItem.Checked);
        menu.Items.Add(startupItem);

        menu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitApplication();
        menu.Items.Add(exitItem);

        return menu;
    }

    private void RebuildPerMonitorMenu(ToolStripMenuItem parent)
    {
        parent.DropDownItems.Clear();

        var secondaryScreens = Screen.AllScreens.Where(s => !s.Primary).ToList();
        if (secondaryScreens.Count == 0)
        {
            parent.DropDownItems.Add(new ToolStripMenuItem("No secondary monitors detected") { Enabled = false });
            return;
        }

        foreach (var screen in secondaryScreens)
        {
            var deviceName = screen.DeviceName;
            var label = $"{deviceName} ({screen.Bounds.Width}x{screen.Bounds.Height})";
            var item = new ToolStripMenuItem(label);
            item.Click += (_, _) => SetPerMonitorImage(deviceName);
            parent.DropDownItems.Add(item);
        }
    }

    private void SetDefaultImage()
    {
        var path = PromptForImage();
        if (path is null) return;

        _settings.DefaultImagePath = path;
        _settings.Save();
    }

    private void SetPerMonitorImage(string deviceName)
    {
        var path = PromptForImage();
        if (path is null) return;

        _settings.PerMonitorImages[deviceName] = path;
        _settings.Save();
    }

    private static string? PromptForImage()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Image files|*.jpg;*.jpeg;*.png;*.bmp|All files|*.*",
            Title = "Choose a wallpaper image",
        };

        return dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : null;
    }

    /// <summary>Lets the user see the overlay without actually locking the workstation.</summary>
    private void PreviewOverlays()
    {
        _settings = AppSettings.Load();
        _overlayManager.ShowAll(_settings);
        _previewTimer.Stop();
        _previewTimer.Start();
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        var elapsedMs = _sinceLastEvent.ElapsedMilliseconds;
        _sinceLastEvent.Restart();
        LogEvent($"{e.Reason} (+{elapsedMs} ms since previous event)");

        switch (e.Reason)
        {
            case SessionSwitchReason.SessionLock:
                _settings = AppSettings.Load();
                _overlayManager.ShowAll(_settings);
                LogEvent("  overlay shown");
                break;
            case SessionSwitchReason.SessionUnlock:
                _overlayManager.HideAll();
                LogEvent("  overlay hidden");
                break;
        }
    }

    private static void LogEvent(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff}  {message}{Environment.NewLine}");
        }
        catch
        {
            // Logging must never take the app down.
        }
    }

    private static void OpenEventLog()
    {
        if (!File.Exists(LogPath))
        {
            MessageBox.Show(
                "No lock/unlock events have been logged yet. Lock the workstation (Win+L) at least once first.",
                "Lock Screen Wallpaper",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        Process.Start(new ProcessStartInfo(LogPath) { UseShellExecute = true });
    }

    private static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(RunValueName) is not null;
    }

    private static void SetStartupEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath)!;

        if (enabled)
        {
            key.SetValue(RunValueName, $"\"{Application.ExecutablePath}\"");
        }
        else
        {
            key.DeleteValue(RunValueName, throwOnMissingValue: false);
        }
    }

    private void ExitApplication()
    {
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        _overlayManager.Dispose();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        ExitThread();
    }
}
