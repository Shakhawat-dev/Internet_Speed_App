using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using InternetSpeedApp.Interop;

namespace InternetSpeedApp.UI;

/// <summary>Draws the 16×16 tray icon (green ↓ arrow, orange ↑ arrow).</summary>
internal static class TrayIconFactory
{
    internal static Icon Create()
    {
        using var bmp = new Bitmap(16, 16, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            // Download arrow (green, left side)
            using var dBrush = new SolidBrush(Color.FromArgb(60, 220, 60));
            g.FillPolygon(dBrush, (PointF[])[new(1, 3), new(7, 3), new(4, 10)]);

            // Upload arrow (orange, right side)
            using var uBrush = new SolidBrush(Color.FromArgb(255, 160, 30));
            g.FillPolygon(uBrush, (PointF[])[new(9, 12), new(15, 12), new(12, 5)]);
        }

        // GetHicon leaks unless the handle is explicitly destroyed after cloning.
        var hIcon = bmp.GetHicon();
        var icon  = (Icon)Icon.FromHandle(hIcon).Clone();
        NativeMethods.DestroyIcon(hIcon);
        return icon;
    }
}
