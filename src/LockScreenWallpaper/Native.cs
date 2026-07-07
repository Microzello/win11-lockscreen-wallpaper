using System.Runtime.InteropServices;

namespace LockScreenWallpaper;

internal static class Native
{
    // Extended window styles used to hide the overlay from Alt+Tab / taskbar
    // and stop it from ever taking keyboard focus.
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_NOACTIVATE = 0x08000000;
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_LAYERED = 0x00080000;

    public const int GWL_EXSTYLE = -20;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}
