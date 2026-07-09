using System.Runtime.InteropServices;

namespace InternetSpeedApp.Interop;

/// <summary>All Win32 P/Invoke declarations, structs, and constants in one place.</summary>
internal static class NativeMethods
{
    // ── Window styles ───────────────────────────────────────────────────────

    internal const int  GWL_STYLE          = -16;
    internal const int  GWL_EXSTYLE        = -20;
    internal const int  WS_CHILD           = 0x40000000;
    internal const int  WS_POPUP           = unchecked((int)0x80000000);
    internal const int  WS_EX_TRANSPARENT  = 0x00000020;
    internal const int  WS_EX_TOOLWINDOW   = 0x00000080;
    internal const int  WS_EX_LAYERED      = 0x00080000;
    internal const int  WS_EX_NOACTIVATE   = 0x08000000;

    // ── UpdateLayeredWindow ─────────────────────────────────────────────────

    internal const uint ULW_ALPHA    = 2;
    internal const byte AC_SRC_OVER  = 0;
    internal const byte AC_SRC_ALPHA = 1;

    [StructLayout(LayoutKind.Sequential)] internal struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] internal struct SIZE  { public int cx, cy; }
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }

    [DllImport("user32.dll")] internal static extern bool UpdateLayeredWindow(
        IntPtr hWnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize,
        IntPtr hdcSrc, ref POINT pptSrc, uint crKey, ref BLENDFUNCTION pblend, uint dwFlags);

    [DllImport("gdi32.dll")]  internal static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")]  internal static extern bool   DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")]  internal static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);
    [DllImport("gdi32.dll")]  internal static extern bool   DeleteObject(IntPtr h);
    [DllImport("user32.dll")] internal static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] internal static extern int    ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("user32.dll")] internal static extern int    GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll")] internal static extern int    SetWindowLong(IntPtr hwnd, int index, int value);
    [DllImport("user32.dll")] internal static extern bool   DestroyIcon(IntPtr hIcon);

    // ── Taskbar embedding ───────────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr FindWindow(string? className, string? windowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string? className, string? windowName);

    [DllImport("user32.dll")] internal static extern IntPtr SetParent(IntPtr child, IntPtr newParent);
    [DllImport("user32.dll")] internal static extern IntPtr GetParent(IntPtr hwnd);
    [DllImport("user32.dll")] internal static extern bool   GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] internal static extern bool   MoveWindow(IntPtr hwnd, int x, int y, int w, int h, bool repaint);
    [DllImport("user32.dll")] internal static extern bool   IsWindow(IntPtr hwnd);

    // ── DWM ─────────────────────────────────────────────────────────────────

    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    // ── Convenience helpers ─────────────────────────────────────────────────

    /// <summary>Requests Windows 11 rounded corners for the window.</summary>
    internal static void ApplyRoundedCorners(IntPtr hwnd)
    {
        int pref = DWMWCP_ROUND;
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));
    }

    /// <summary>Toggles WS_EX_TRANSPARENT so the window ignores all mouse input.</summary>
    internal static void SetClickThrough(IntPtr hwnd, bool on)
    {
        int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        _ = SetWindowLong(hwnd, GWL_EXSTYLE,
            on ? ex | WS_EX_TRANSPARENT : ex & ~WS_EX_TRANSPARENT);
    }
}
