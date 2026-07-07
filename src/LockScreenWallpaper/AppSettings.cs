using System.Text.Json;

namespace LockScreenWallpaper;

internal sealed class AppSettings
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LockScreenWallpaper");

    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

    // Fallback image used for any secondary monitor that has no entry in PerMonitorImages.
    public string? DefaultImagePath { get; set; }

    // Keyed by Screen.DeviceName (e.g. "\\.\DISPLAY2") for monitors that should show a distinct image.
    public Dictionary<string, string> PerMonitorImages { get; set; } = new();

    public bool RunAtStartup { get; set; }

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings is not null)
                    return settings;
            }
        }
        catch
        {
            // Corrupt or unreadable settings file: fall back to defaults rather than crash the tray app.
        }

        return new AppSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(SettingsDir);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }

    public string? ResolveImageFor(string deviceName)
        => PerMonitorImages.TryGetValue(deviceName, out var path) ? path : DefaultImagePath;
}
