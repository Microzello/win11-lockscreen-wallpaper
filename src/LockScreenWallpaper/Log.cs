namespace LockScreenWallpaper;

/// <summary>
/// Tiny append-only log used for diagnosing lock/unlock timing and, more
/// importantly, exceptions we catch instead of letting them kill the app
/// (see Program.cs and OverlayManager.cs).
/// </summary>
internal static class Log
{
    public static readonly string FilePath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LockScreenWallpaper", "events.log");

    public static void Write(string message)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(FilePath)!);
            File.AppendAllText(FilePath, $"{DateTime.Now:HH:mm:ss.fff}  {message}{Environment.NewLine}");
        }
        catch
        {
            // Logging must never take the app down.
        }
    }
}
