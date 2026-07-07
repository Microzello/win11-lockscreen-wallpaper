using Microsoft.Win32;

namespace LockScreenWallpaper;

/// <summary>
/// Creates and tears down one <see cref="OverlayForm"/> per non-primary monitor.
/// </summary>
internal sealed class OverlayManager : IDisposable
{
    private readonly List<OverlayForm> _overlays = new();
    private AppSettings? _activeSettings;

    public OverlayManager()
    {
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    public bool IsShowing => _overlays.Count > 0;

    public void ShowAll(AppSettings settings)
    {
        HideAllCore();
        _activeSettings = settings;

        foreach (var screen in Screen.AllScreens)
        {
            if (screen.Primary)
                continue; // The real lock/sign-in screen already renders here.

            var imagePath = settings.ResolveImageFor(screen.DeviceName);
            var overlay = new OverlayForm(screen.Bounds, imagePath);
            _overlays.Add(overlay);
            overlay.Show();
        }
    }

    public void HideAll()
    {
        HideAllCore();
        _activeSettings = null;
    }

    private void HideAllCore()
    {
        foreach (var overlay in _overlays)
        {
            overlay.Close();
            overlay.Dispose();
        }

        _overlays.Clear();
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        // A monitor was plugged/unplugged or rearranged while locked, so rebuild to match.
        if (_activeSettings is { } settings)
        {
            ShowAll(settings);
        }
    }

    public void Dispose()
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        HideAllCore();
    }
}
