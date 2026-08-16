using System.Drawing;
using System.Windows.Forms;

namespace Otoink.App.Tray;

/// <summary>
/// System tray icon for hide/show/exit. Tooltip text is "otoink".
/// </summary>
public sealed class NotifyIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private Icon? _ownedIcon;
    private bool _disposed;

    public NotifyIconService()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("显示", null, (_, _) => ShowRequested?.Invoke());
        menu.Items.Add("退出", null, (_, _) => ExitRequested?.Invoke());

        _ownedIcon = CreateTrayIcon();
        _notifyIcon = new NotifyIcon
        {
            Text = "otoink",
            Icon = _ownedIcon,
            Visible = true,
            ContextMenuStrip = menu
        };
        _notifyIcon.MouseClick += OnMouseClick;
    }

    public event Action? ShowRequested;
    public event Action? ExitRequested;

    private void OnMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
            ShowRequested?.Invoke();
    }

    private static Icon CreateTrayIcon()
    {
        using var bmp = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            using var brush = new SolidBrush(Color.FromArgb(0xE5, 0x39, 0x35));
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.FillEllipse(brush, 1, 1, 14, 14);
            using var mic = new SolidBrush(Color.White);
            g.FillRectangle(mic, 7, 4, 2, 5);
            g.FillEllipse(mic, 6, 8, 4, 3);
            g.FillRectangle(mic, 7, 11, 2, 2);
        }

        var hIcon = bmp.GetHicon();
        try
        {
            // Clone so the icon owns its data after DestroyIcon.
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

        _notifyIcon.MouseClick -= OnMouseClick;
        _notifyIcon.Visible = false;
        _notifyIcon.ContextMenuStrip?.Dispose();
        _notifyIcon.Dispose();
        _ownedIcon?.Dispose();
        _ownedIcon = null;
        ShowRequested = null;
        ExitRequested = null;
    }
}
