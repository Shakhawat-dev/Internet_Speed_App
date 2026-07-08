using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace InternetSpeedApp;

internal sealed class SpeedWindow : Form
{
    private const string AppName = "InternetSpeedApp";
    private const string RunKey  = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    // ── Win32 P/Invoke ───────────────────────────────────────────────────────

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    [DllImport("user32.dll")] private static extern bool UpdateLayeredWindow(
        IntPtr hWnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize,
        IntPtr hdcSrc, ref POINT pptSrc, uint crKey, ref BLENDFUNCTION pblend, uint dwFlags);
    [DllImport("gdi32.dll")]  private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")]  private static extern bool   DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")]  private static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);
    [DllImport("gdi32.dll")]  private static extern bool   DeleteObject(IntPtr h);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern int    ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] struct SIZE  { public int cx, cy; }
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }

    private const uint ULW_ALPHA    = 2;
    private const byte AC_SRC_OVER  = 0;
    private const byte AC_SRC_ALPHA = 1;

    // ── Fields ────────────────────────────────────────────────────────────────

    private readonly System.Windows.Forms.Timer _timer;
    private readonly ToolStripMenuItem _startupItem;
    private readonly ToolStripMenuItem _alwaysOnTopItem;

    private AppSettings  _settings;
    private Font         _font = new("Segoe UI", 13f, FontStyle.Bold);
    private RectangleF   _downRect, _upRect;
    private string       _downText = "↓  —";
    private string       _upText   = "↑  —";

    private long     _lastReceived, _lastSent;
    private DateTime _lastSampleTime;
    private bool     _dragging;
    private Point    _mouseDownScreen, _locationAtMouseDown;

    // ── Construction ─────────────────────────────────────────────────────────

    internal SpeedWindow()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition   = FormStartPosition.Manual;
        ShowInTaskbar   = false;
        BackColor       = Color.Black;

        _startupItem = new ToolStripMenuItem("Start with Windows") { Checked = IsAutoStart() };
        _startupItem.Click += (_, _) => ToggleAutoStart(_startupItem);

        _alwaysOnTopItem = new ToolStripMenuItem("Always on Top") { Checked = true };
        _alwaysOnTopItem.Click += (_, _) =>
        {
            _settings.AlwaysOnTop    = !_settings.AlwaysOnTop;
            _alwaysOnTopItem.Checked = _settings.AlwaysOnTop;
            TopMost                  = _settings.AlwaysOnTop;
            _settings.Save();
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add(_startupItem);
        menu.Items.Add(_alwaysOnTopItem);
        menu.Items.Add("Settings…", null, (_, _) => OpenSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Application.Exit());
        ContextMenuStrip = menu;

        MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            _dragging            = true;
            _mouseDownScreen     = MousePosition;
            _locationAtMouseDown = Location;
        };
        MouseMove += (_, _) =>
        {
            if (!_dragging) return;
            Location = new Point(
                _locationAtMouseDown.X + MousePosition.X - _mouseDownScreen.X,
                _locationAtMouseDown.Y + MousePosition.Y - _mouseDownScreen.Y);
            Render();
        };
        MouseUp += (_, e) => { if (e.Button == MouseButtons.Left) _dragging = false; };

        (_lastReceived, _lastSent) = NetworkStats.GetTotals();
        _lastSampleTime = DateTime.UtcNow;

        _timer = new System.Windows.Forms.Timer { Interval = 1000 };
        _timer.Tick += OnTick;
        _timer.Start();

        _settings = AppSettings.Load();
        ApplySettings(_settings, firstRun: true);
        UpdateSpeedText(0, 0);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int pref = DWMWCP_ROUND;
        DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));
        Render();
    }

    // ── Settings ─────────────────────────────────────────────────────────────

    private void OpenSettings()
    {
        using var form = new SettingsForm(_settings);
        if (form.ShowDialog(this) != DialogResult.OK) return;
        _settings = form.Result;
        _settings.Save();
        ApplySettings(_settings, firstRun: false);
    }

    private void ApplySettings(AppSettings s, bool firstRun)
    {
        TopMost                  = s.AlwaysOnTop;
        _alwaysOnTopItem.Checked = s.AlwaysOnTop;
        _font                    = new Font("Segoe UI", s.FontSize, FontStyle.Bold);

        var probe = s.DecimalUnits ? "↓  999.9 MB/s" : "↓  999.9 MiB/s";
        var sz = TextRenderer.MeasureText(probe, _font);
        int lw = sz.Width + 10;
        int lh = sz.Height + 6;

        if (s.Horizontal)
        {
            _downRect  = new RectangleF(10, 8, lw, lh);
            _upRect    = new RectangleF(10 + lw + 8, 8, lw, lh);
            ClientSize = new Size(10 + lw * 2 + 8 + 10, lh + 16);
        }
        else
        {
            _downRect  = new RectangleF(10, 8, lw, lh);
            _upRect    = new RectangleF(10, 8 + lh + 4, lw, lh);
            ClientSize = new Size(lw + 20, 8 + lh * 2 + 4 + 8);
        }

        if (firstRun)
        {
            var wa = Screen.PrimaryScreen!.WorkingArea;
            Location = new Point(wa.Right - Width - 16, wa.Bottom - Height - 16);
        }

        if (IsHandleCreated) Render();
    }

    // ── Rendering ────────────────────────────────────────────────────────────

    private void Render()
    {
        if (!IsHandleCreated || Width <= 0 || Height <= 0) return;

        int bgA   = Clamp255(_settings.BackgroundOpacity);
        int textA = Clamp255(_settings.TextOpacity);

        using var bmp = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

            using var bgBrush     = new SolidBrush(Color.FromArgb(bgA, 18, 18, 18));
            using var borderPen   = new Pen(Color.FromArgb(bgA, 50, 50, 50));
            g.FillRectangle(bgBrush, 0, 0, Width, Height);
            g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);

            var downBase = Color.FromArgb(_settings.DownloadColor);
            var upBase   = Color.FromArgb(_settings.UploadColor);
            using var downBrush = new SolidBrush(Color.FromArgb(textA, downBase.R, downBase.G, downBase.B));
            using var upBrush   = new SolidBrush(Color.FromArgb(textA, upBase.R,   upBase.G,   upBase.B));

            var fmt = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
            g.DrawString(_downText, _font, downBrush, _downRect, fmt);
            g.DrawString(_upText,   _font, upBrush,   _upRect,   fmt);
        }

        PremultiplyAlpha(bmp);
        BlitToScreen(bmp);
    }

    private static int Clamp255(double v) => Math.Clamp((int)(v * 255), 0, 255);

    // Layered windows with AC_SRC_ALPHA require pre-multiplied alpha.
    private static void PremultiplyAlpha(Bitmap bmp)
    {
        var data   = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height),
                         ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        int bytes  = Math.Abs(data.Stride) * bmp.Height;
        var buffer = new byte[bytes];
        Marshal.Copy(data.Scan0, buffer, 0, bytes);
        for (int i = 0; i < bytes; i += 4)
        {
            byte a = buffer[i + 3];
            if (a < 255)
            {
                buffer[i]     = (byte)(buffer[i]     * a / 255);
                buffer[i + 1] = (byte)(buffer[i + 1] * a / 255);
                buffer[i + 2] = (byte)(buffer[i + 2] * a / 255);
            }
        }
        Marshal.Copy(buffer, 0, data.Scan0, bytes);
        bmp.UnlockBits(data);
    }

    private void BlitToScreen(Bitmap bmp)
    {
        var screenDc = GetDC(IntPtr.Zero);
        var memDc    = CreateCompatibleDC(screenDc);
        var hBmp     = bmp.GetHbitmap(Color.FromArgb(0));
        var hOld     = SelectObject(memDc, hBmp);

        var blend  = new BLENDFUNCTION { BlendOp = AC_SRC_OVER, AlphaFormat = AC_SRC_ALPHA, SourceConstantAlpha = 255 };
        var size   = new SIZE  { cx = Width, cy = Height };
        var srcPt  = new POINT { X  = 0,     Y  = 0 };
        var dstPt  = new POINT { X  = Left,  Y  = Top };

        UpdateLayeredWindow(Handle, screenDc, ref dstPt, ref size, memDc, ref srcPt, 0, ref blend, ULW_ALPHA);

        SelectObject(memDc, hOld);
        DeleteObject(hBmp);
        DeleteDC(memDc);
        ReleaseDC(IntPtr.Zero, screenDc);
    }

    // ── Network sampling ─────────────────────────────────────────────────────

    private void OnTick(object? sender, EventArgs e)
    {
        var (received, sent) = NetworkStats.GetTotals();
        var now     = DateTime.UtcNow;
        var elapsed = (now - _lastSampleTime).TotalSeconds;
        long down = 0, up = 0;
        if (elapsed > 0)
        {
            down = Math.Max(0, (long)((received - _lastReceived) / elapsed));
            up   = Math.Max(0, (long)((sent - _lastSent) / elapsed));
        }
        _lastReceived   = received;
        _lastSent       = sent;
        _lastSampleTime = now;
        UpdateSpeedText(down, up);
    }

    private void UpdateSpeedText(long downBps, long upBps)
    {
        _downText = $"↓  {FormatSpeed(downBps)}";
        _upText   = $"↑  {FormatSpeed(upBps)}";
        if (IsHandleCreated) Render();
    }

    private string FormatSpeed(long bps)
    {
        int div      = _settings.DecimalUnits ? 1000 : 1024;
        string kUnit = _settings.DecimalUnits ? "KB/s"  : "KiB/s";
        string mUnit = _settings.DecimalUnits ? "MB/s"  : "MiB/s";
        if (bps >= (long)div * div) return $"{bps / ((double)div * div):F1} {mUnit}";
        if (bps >= div)             return $"{bps / (double)div:F0} {kUnit}";
        return $"{bps} B/s";
    }

    // ── Win32 hints ──────────────────────────────────────────────────────────

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x00080000; // WS_EX_LAYERED
            cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
            cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
            return cp;
        }
    }

    // ── Autostart ────────────────────────────────────────────────────────────

    private static bool IsAutoStart()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(AppName) is not null;
    }

    private static void ToggleAutoStart(ToolStripMenuItem item)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key is null) return;
        if (item.Checked) { key.DeleteValue(AppName, false); item.Checked = false; }
        else              { key.SetValue(AppName, Application.ExecutablePath); item.Checked = true; }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _timer.Dispose(); _font.Dispose(); }
        base.Dispose(disposing);
    }
}
