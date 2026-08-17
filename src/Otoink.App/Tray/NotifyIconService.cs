using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace Otoink.App.Tray;

/// <summary>
/// Tray icon. Left-click opens settings; right-click raises a custom menu request.
/// Tooltip text is "otoink".
/// </summary>
public sealed class NotifyIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private Icon? _ownedIcon;
    private bool _disposed;

    public NotifyIconService()
    {
        _ownedIcon = CreateTrayIcon();
        _notifyIcon = new NotifyIcon
        {
            Text = "otoink",
            Icon = _ownedIcon,
            Visible = true
        };
        _notifyIcon.MouseUp += OnMouseUp;
    }

    public event Action? SettingsRequested;
    public event Action<Point>? MenuRequested;

    private void OnMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
            SettingsRequested?.Invoke();
        else if (e.Button == MouseButtons.Right)
            MenuRequested?.Invoke(Cursor.Position);
    }

    private static Icon CreateTrayIcon()
    {
        try
        {
            var icoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "otoink.ico");
            if (File.Exists(icoPath))
            {
                using var file = new Icon(icoPath, 32, 32);
                return (Icon)file.Clone();
            }

            var jpgPath = Path.Combine(AppContext.BaseDirectory, "Assets", "otoink-logo.jpg");
            if (File.Exists(jpgPath))
                return FromBitmapFile(jpgPath);
        }
        catch
        {
            // Fall through to the drawn mark.
        }

        using var bmp = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            using var brush = new SolidBrush(Color.FromArgb(0xE5, 0x39, 0x35));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.FillEllipse(brush, 1, 1, 14, 14);
        }

        return IconFromBitmap(bmp);
    }

    private static Icon FromBitmapFile(string path)
    {
        using var src = new Bitmap(path);
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.Clear(Color.Transparent);
            g.DrawImage(src, 0, 0, 32, 32);
        }

        return IconFromBitmap(bmp);
    }

    private static Icon IconFromBitmap(Bitmap bmp)
    {
        var hIcon = bmp.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(hIcon);
            return (Icon)temp.Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _notifyIcon.MouseUp -= OnMouseUp;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _ownedIcon?.Dispose();
        _ownedIcon = null;
        SettingsRequested = null;
        MenuRequested = null;
    }
}
