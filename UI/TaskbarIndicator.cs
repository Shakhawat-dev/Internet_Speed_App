using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using InternetSpeedApp.Core;
using InternetSpeedApp.Interop;

namespace InternetSpeedApp.UI;

/// <summary>
/// A small two-line ↑/↓ speed readout that appears inside the taskbar, just
/// left of the system tray — the style NetSpeedMonitor made popular.
///
/// The Windows 11 taskbar is a DirectComposition surface that does not render
/// foreign child HWNDs, so true embedding is impossible; instead this is a
/// topmost layered window positioned over the taskbar strip. Per-pixel alpha
/// makes the background fully transparent (the taskbar shows through) while
/// the text stays solid, so it looks native.
/// </summary>
internal sealed class TaskbarIndicator : Form
{
    private readonly SpeedWindow _owner;
    private readonly Font _font = new("Segoe UI", 8.25f, FontStyle.Bold);

    private IntPtr _taskbar;
    private string _upText   = "↑: —";
    private string _downText = "↓: —";
    private int    _ticksSinceReposition;

    private bool _dragging;
    private int  _dragStartCursorX, _dragStartWindowX;

    internal TaskbarIndicator(SpeedWindow owner, ContextMenuStrip menu)
    {
        _owner = owner;

        FormBorderStyle  = FormBorderStyle.None;
        StartPosition    = FormStartPosition.Manual;
        ShowInTaskbar    = false;
        TopMost          = true;
        ContextMenuStrip = menu;
        Size             = new Size(96, 48);
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= NativeMethods.WS_EX_LAYERED;
            cp.ExStyle |= NativeMethods.WS_EX_NOACTIVATE;
            cp.ExStyle |= NativeMethods.WS_EX_TOOLWINDOW;
            return cp;
        }
    }

    /// <summary>Positions the overlay over the taskbar and shows it.</summary>
    internal void ShowInsideTaskbar()
    {
        Reposition();
        Show();
        Render();
    }

    /// <summary>Sizes to the taskbar strip and docks just left of the tray area.</summary>
    private void Reposition()
    {
        _taskbar = NativeMethods.FindWindow("Shell_TrayWnd", null);
        if (_taskbar == IntPtr.Zero || !NativeMethods.GetWindowRect(_taskbar, out var tb))
            return;

        int width  = TextRenderer.MeasureText("↑: 999.9 MiB/s", _font).Width + 10;
        int height = tb.Bottom - tb.Top;

        // Dock left of the tray area (user-adjustable gap — drag the readout to
        // clear XAML-drawn taskbar content like the weather widget, which has
        // no HWND to measure against). Fall back to a fixed right-edge gap.
        int gap = Math.Max(0, _owner.Settings.TaskbarIndicatorGap);
        var tray = NativeMethods.FindWindowEx(_taskbar, IntPtr.Zero, "TrayNotifyWnd", null);
        int x = tray != IntPtr.Zero && NativeMethods.GetWindowRect(tray, out var tr)
            ? tr.Left - width - gap
            : tb.Right - 220 - width - gap;

        Bounds = new Rectangle(x, tb.Top, width, height);
    }

    /// <summary>Called by the owner each sample tick.</summary>
    internal void UpdateSpeeds(string upText, string downText)
    {
        _upText   = upText;
        _downText = downText;

        // The tray area can grow/shrink and the taskbar can move (DPI change);
        // periodically re-dock and re-assert topmost so the taskbar never
        // covers the overlay after Explorer restarts. Never fight a drag.
        if (!_dragging && ++_ticksSinceReposition >= 5)
        {
            _ticksSinceReposition = 0;
            Reposition();
            TopMost = true;
        }

        if (IsHandleCreated && Visible) Render();
    }

    private void Render()
    {
        if (!IsHandleCreated || Width <= 0 || Height <= 0) return;

        var s = _owner.Settings;

        using var bmp = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            g.SmoothingMode     = SmoothingMode.AntiAlias;

            // Opaque dark background for clean antialiasing; the presenter then
            // knocks it down to alpha 1 — visually transparent, but still
            // hit-testable so right-click anywhere on the readout works.
            g.Clear(LayeredWindowPresenter.BackgroundColor);

            using var upBrush   = new SolidBrush(Color.FromArgb(s.UploadColor));
            using var downBrush = new SolidBrush(Color.FromArgb(s.DownloadColor));

            float lineH = Height / 2f;
            var fmt = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
            g.DrawString(_upText,   _font, upBrush,   new RectangleF(4, 0,     Width - 4, lineH), fmt);
            g.DrawString(_downText, _font, downBrush, new RectangleF(4, lineH, Width - 4, lineH), fmt);
        }

        LayeredWindowPresenter.Present(Handle, bmp, Location, bgAlpha: 1, textAlpha: 255);
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (e.Button == MouseButtons.Left) _owner.ToggleWidgetVisible();
    }

    // ── Horizontal drag to reposition along the taskbar ─────────────────────

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        _dragging         = true;
        _dragStartCursorX = MousePosition.X;
        _dragStartWindowX = Left;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_dragging) return;
        Left = _dragStartWindowX + MousePosition.X - _dragStartCursorX;
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left || !_dragging) return;
        _dragging = false;

        // Persist only real drags — a plain click must not clobber the gap.
        if (Math.Abs(Left - _dragStartWindowX) < 3) return;

        var tray = NativeMethods.FindWindowEx(_taskbar, IntPtr.Zero, "TrayNotifyWnd", null);
        if (tray != IntPtr.Zero && NativeMethods.GetWindowRect(tray, out var tr))
            _owner.SaveTaskbarIndicatorGap(Math.Max(0, tr.Left - Right));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _font.Dispose();
        base.Dispose(disposing);
    }
}
