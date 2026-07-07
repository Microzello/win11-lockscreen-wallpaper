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
        Application.Run(new TrayApplicationContext());
    }
}
