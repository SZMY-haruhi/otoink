using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Otoink.App.Win32;

namespace Otoink.App.Design;

internal static class WindowCapture
{
    public static string Save(IEnumerable<IntPtr> hwnds, string path)
    {
        var handles = hwnds.Where(h => h != IntPtr.Zero).Distinct().ToArray();
        if (handles.Length == 0)
            throw new InvalidOperationException("No window to capture.");

        var rects = handles.Select(h =>
        {
            NativeMethods.GetWindowRect(h, out var rect);
            return (Hwnd: h, Rect: rect);
        }).Where(x => x.Rect.Width > 0 && x.Rect.Height > 0).ToArray();

        if (rects.Length == 0)
            throw new InvalidOperationException("Window size is 0.");

        var left = rects.Min(x => x.Rect.Left);
        var top = rects.Min(x => x.Rect.Top);
        var right = rects.Max(x => x.Rect.Right);
        var bottom = rects.Max(x => x.Rect.Bottom);
        var width = right - left;
        var height = bottom - top;

        using var canvas = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(canvas))
        {
            g.Clear(Color.FromArgb(255, 18, 21, 28));
            foreach (var (hwnd, rect) in rects)
            {
                using var piece = CaptureHwnd(hwnd, rect.Width, rect.Height);
                g.DrawImageUnscaled(piece, rect.Left - left, rect.Top - top);
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        canvas.Save(path, ImageFormat.Png);
        return path;
    }

    private static Bitmap CaptureHwnd(IntPtr hwnd, int width, int height)
    {
        var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        var hdc = g.GetHdc();
        try
        {
            if (!NativeMethods.PrintWindow(hwnd, hdc, NativeMethods.PW_RENDERFULLCONTENT))
                NativeMethods.PrintWindow(hwnd, hdc, 0);
        }
        finally
        {
            g.ReleaseHdc(hdc);
        }

        return bmp;
    }
}
