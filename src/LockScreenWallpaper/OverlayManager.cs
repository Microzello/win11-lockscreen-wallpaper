using Microsoft.Win32;

namespace LockScreenWallpaper;

/// <summary>
/// Creates and tears down one <see cref="OverlayForm"/> per non-primary monitor.
/// </summary>
internal sealed class OverlayManager : IDisposable
{
    private readonly List<OverlayForm> _overlays = new();
    private readonly System.Windows.Forms.Timer _rebuildDebounceTimer;
    private AppSettings? _activeSettings;
    private bool _isRebuilding;

    public OverlayManager()
    {
        // See OnDisplaySettingsChanged for why this is debounced instead of
        // rebuilding inline.
        _rebuildDebounceTimer = new System.Windows.Forms.Timer { Interval = 750 };
        _rebuildDebounceTimer.Tick += (_, _) =>
        {
            _rebuildDebounceTimer.Stop();
            if (_activeSettings is { } settings)
            {
                RebuildNow(settings);
            }
        };

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    public bool IsShowing => _overlays.Count > 0;

    public void ShowAll(AppSettings settings)
    {
        _activeSettings = settings;
        RebuildNow(settings);
    }

    public void HideAll()
    {
        _rebuildDebounceTimer.Stop();
        HideAllCore();
        _activeSettings = null;
    }

    private void RebuildNow(AppSettings settings)
    {
        // Hot-plugging a monitor can fire DisplaySettingsChanged several
        // times in quick succession while Windows renegotiates the topology.
        // Without this guard, a burst of events could re-enter this method
        // while a previous call is still tearing down/rebuilding overlays.
        // The in-progress call already reads Screen.AllScreens fresh each
        // time, so dropping the reentrant call is safe -- nothing is lost,
        // it just avoids double-building.
        if (_isRebuilding)
        {
            return;
        }

        _isRebuilding = true;
        try
        {
            HideAllCore();

            foreach (var screen in Screen.AllScreens)
            {
                if (screen.Primary)
                    continue; // The real lock/sign-in screen already renders here.

                try
                {
                    var imagePath = settings.ResolveImageFor(screen.DeviceName);
                    var overlay = new OverlayForm(screen.Bounds, imagePath);
                    _overlays.Add(overlay);
                    overlay.Show();
                }
                catch (Exception ex)
                {
                    // A single monitor failing to get an overlay (e.g. its
                    // geometry is mid-change during a hot-plug) shouldn't take
                    // the rest of the app down.
                    Log.Write($"Failed to create overlay for {screen.DeviceName}: {ex}");
                }
            }
        }
        finally
        {
            _isRebuilding = false;
        }
    }

    private void HideAllCore()
    {
        // Snapshot and clear before closing anything: if closing one overlay
        // somehow re-enters this class (directly or via a queued
        // DisplaySettingsChanged event), it will see an already-empty list
        // instead of mutating the collection this loop is enumerating.
        var overlaysToClose = _overlays.ToArray();
        _overlays.Clear();

        foreach (var overlay in overlaysToClose)
        {
            try
            {
                overlay.Close();
                overlay.Dispose();
            }
            catch (Exception ex)
            {
                Log.Write($"Failed to close an overlay: {ex}");
            }
        }
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        // A monitor was plugged/unplugged or rearranged while locked, so rebuild to match.
        //
        // This notification is broadcast to every top-level window, and (per
        // a hang observed in testing: plugging a second monitor in while
        // locked left the process alive but unresponsive, tray icon gone
        // ghost) creating a new always-on-top, uiAccess-privileged window
        // synchronously from inside that broadcast handler appears to be
        // able to deadlock with the shell coordinating that window's special
        // z-order band. Restarting a timer here instead defers the actual
        // rebuild to a fresh top-of-message-loop dispatch (a WM_TIMER, not a
        // nested sent message), and doubles as a debounce for the burst of
        // DisplaySettingsChanged events a hot-plug tends to fire while the
        // topology is still settling.
        if (_activeSettings is not null)
        {
            _rebuildDebounceTimer.Stop();
            _rebuildDebounceTimer.Start();
        }
    }

    public void Dispose()
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _rebuildDebounceTimer.Stop();
        _rebuildDebounceTimer.Dispose();
        HideAllCore();
    }
}
