namespace LockScreenWallpaper;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Prevent a second copy from registering a duplicate tray icon and a duplicate
        // SessionSwitch handler (which would show two overlapping overlay windows).
        using var singleInstanceMutex = new Mutex(initiallyOwned: true, "LockScreenWallpaper.SingleInstance", out var isNewInstance);
        if (!isNewInstance)
        {
            MessageBox.Show(
                "Lock Screen Wallpaper is already running (check the system tray).",
                "Lock Screen Wallpaper",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // Without this, an exception thrown while handling a Windows message
        // (e.g. a monitor hot-plug event) takes the entire process down with
        // no dialog, no tray icon, and no way to relaunch other than digging
        // up the exe manually. Route it to ThreadException and log it
        // instead, so a single bad edge case can't kill the whole app.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Log.Write($"Unhandled UI thread exception: {e.Exception}");
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log.Write($"Unhandled exception (terminating={e.IsTerminating}): {e.ExceptionObject}");

        Application.Run(new TrayApplicationContext());
    }
}
