using System.Drawing.Drawing2D;

namespace LockScreenWallpaper;

/// <summary>
/// A borderless, topmost, Alt+Tab-invisible window that fills exactly one monitor.
/// Windows only paints the lock-screen wallpaper on the primary display; while the
/// user session is locked (and before credential entry switches to the secure
/// desktop), this window fills in the black secondary monitors.
/// </summary>
internal sealed class OverlayForm : Form
{
    private readonly Image? _image;

    public OverlayForm(Rectangle monitorBounds, string? imagePath)
    {
        // Manual painting instead of BackgroundImage/BackgroundImageLayout: WinForms'
        // built-in layouts only offer "contain" (Zoom, letterboxed) or distort (Stretch),
        // not "cover" (fill the screen, cropping overflow), so we draw it ourselves.
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

        SuspendLayout();

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.Black;
        Bounds = monitorBounds;
        Cursor = Cursors.Default;

        if (imagePath is not null)
        {
            _image = LoadImageWithoutLocking(imagePath);
        }

        ResumeLayout(false);
    }

    // Skip Alt+Tab / taskbar entirely and never accept keyboard focus, so this
    // window can never interfere with whatever legitimately has focus.
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE;
            return cp;
        }
    }

    protected override bool ShowWithoutActivation => true;

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        if (_image is null)
        {
            e.Graphics.Clear(Color.Black);
            return;
        }

        // "Fill": scale to cover the whole monitor, preserving aspect ratio, cropping
        // whatever overflows, matches the desktop wallpaper "Fill" style, not "Fit".
        var bounds = ClientRectangle;
        var scale = Math.Max((float)bounds.Width / _image.Width, (float)bounds.Height / _image.Height);
        var drawWidth = (int)Math.Ceiling(_image.Width * scale);
        var drawHeight = (int)Math.Ceiling(_image.Height * scale);
        var x = (bounds.Width - drawWidth) / 2;
        var y = (bounds.Height - drawHeight) / 2;

        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        e.Graphics.DrawImage(_image, new Rectangle(x, y, drawWidth, drawHeight));
    }

    private static Image? LoadImageWithoutLocking(string path)
    {
        try
        {
            using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var buffer = new MemoryStream();
            fileStream.CopyTo(buffer);
            buffer.Position = 0;
            // Image.FromStream keeps the stream open for the lifetime of the Image;
            // MemoryStream has no OS handle, so the source file is never locked.
            return Image.FromStream(buffer);
        }
        catch
        {
            return null;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _image?.Dispose();
        }

        base.Dispose(disposing);
    }
}
