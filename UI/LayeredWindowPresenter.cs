using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using InternetSpeedApp.Interop;

namespace InternetSpeedApp.UI;

/// <summary>
/// Presents an opaque-rendered bitmap as a per-pixel-alpha layered window.
///
/// The widget draws everything fully opaque (so GDI+ antialiases text against
/// solid pixels), then this class applies the user's background/text opacities
/// per pixel — background and border pixels are recognized by exact color —
/// premultiplies (required by UpdateLayeredWindow with AC_SRC_ALPHA), and blits
/// the result to screen.
/// </summary>
internal static class LayeredWindowPresenter
{
    /// <summary>Colors the widget must use so pixels classify as background.</summary>
    internal static readonly Color BackgroundColor = Color.FromArgb(18, 18, 18);
    internal static readonly Color BorderColor     = Color.FromArgb(50, 50, 50);

    internal static void Present(IntPtr handle, Bitmap bmp, Point location, int bgAlpha, int textAlpha)
    {
        ApplyAlphaAndPremultiply(bmp, bgAlpha, textAlpha);
        Blit(handle, bmp, location);
    }

    // Pixel byte order for Format32bppArgb in memory: [B, G, R, A].
    private static void ApplyAlphaAndPremultiply(Bitmap bmp, int bgA, int textA)
    {
        var data  = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height),
                        ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        int bytes = Math.Abs(data.Stride) * bmp.Height;
        var buf   = new byte[bytes];
        Marshal.Copy(data.Scan0, buf, 0, bytes);

        for (int i = 0; i < bytes; i += 4)
        {
            int b = buf[i], g = buf[i + 1], r = buf[i + 2];
            bool isBg = (r == BackgroundColor.R && g == BackgroundColor.G && b == BackgroundColor.B)
                     || (r == BorderColor.R     && g == BorderColor.G     && b == BorderColor.B);
            int a = isBg ? bgA : textA;
            buf[i]     = (byte)(b * a / 255);
            buf[i + 1] = (byte)(g * a / 255);
            buf[i + 2] = (byte)(r * a / 255);
            buf[i + 3] = (byte)a;
        }

        Marshal.Copy(buf, 0, data.Scan0, bytes);
        bmp.UnlockBits(data);
    }

    private static void Blit(IntPtr handle, Bitmap bmp, Point location)
    {
        var screenDc = NativeMethods.GetDC(IntPtr.Zero);
        var memDc    = NativeMethods.CreateCompatibleDC(screenDc);
        var hBmp     = bmp.GetHbitmap(Color.FromArgb(0));
        var hOld     = NativeMethods.SelectObject(memDc, hBmp);

        var blend = new NativeMethods.BLENDFUNCTION
        {
            BlendOp             = NativeMethods.AC_SRC_OVER,
            AlphaFormat         = NativeMethods.AC_SRC_ALPHA,
            SourceConstantAlpha = 255,
        };
        var size  = new NativeMethods.SIZE  { cx = bmp.Width,   cy = bmp.Height };
        var srcPt = new NativeMethods.POINT { X  = 0,           Y  = 0 };
        var dstPt = new NativeMethods.POINT { X  = location.X,  Y  = location.Y };

        NativeMethods.UpdateLayeredWindow(handle, screenDc, ref dstPt, ref size,
            memDc, ref srcPt, 0, ref blend, NativeMethods.ULW_ALPHA);

        NativeMethods.SelectObject(memDc, hOld);
        NativeMethods.DeleteObject(hBmp);
        NativeMethods.DeleteDC(memDc);
        NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
    }
}
