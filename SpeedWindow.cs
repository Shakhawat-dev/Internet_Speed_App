using System.Drawing;
using System.Drawing.Drawing2D;
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
    [DllImport("user32.dll")] private static extern int    GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll")] private static extern int    SetWindowLong(IntPtr hwnd, int index, int value);
    [DllImport("user32.dll")] private static extern bool   DestroyIcon(IntPtr hIcon);

    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] struct SIZE  { public int cx, cy; }
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }

    private const uint ULW_ALPHA      = 2;
    private const byte AC_SRC_OVER   = 0;
    private const byte AC_SRC_ALPHA  = 1;
    private const int  GWL_EXSTYLE   = -20;
    private const int  WS_EX_TRANSPARENT = 0x00000020;

    // ── Sparkline ────────────────────────────────────────────────────────────

    private const int HistLen = 60;
    private const int SparkH  = 36;   // total height of sparkline area

    private readonly long[] _downHist = new long[HistLen];
    private readonly long[] _upHist   = new long[HistLen];
    private int       _histIdx;
    private Rectangle _sparkRect;

    // ── Fields ────────────────────────────────────────────────────────────────

    private readonly System.Windows.Forms.Timer _timer;
    private readonly ToolStripMenuItem _startupItem;
    private readonly ToolStripMenuItem _alwaysOnTopItem;
    private readonly ToolStripMenuItem _clickThroughItem;
    private readonly ToolStripMenuItem _showHideItem;
    private readonly ToolStripMenuItem _sessionItem;
    private readonly NotifyIcon        _trayIcon;

    // Cumulative bytes transferred since the app started (this session).
    private long _sessionDown, _sessionUp;

    // Most recent per-second speeds (bytes/s).
    private long _curDown, _curUp;

    // Persistent daily/monthly usage history.
    private readonly UsageTracker _usage = UsageTracker.Load();
    private int _ticksSinceUsageSave;

    // Live ping + connection-down alerting.
    private readonly PingMonitor _ping = new();
    private bool _wasUp = true;

    // Monthly-cap notifications (fired once each per session).
    private bool _capWarned80, _capWarned100;

    private DashboardForm?    _dashboard;
    private UsageHistoryForm? _historyForm;

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

        // ── Menu items ───────────────────────────────────────────────────────

        _showHideItem = new ToolStripMenuItem("Hide Widget");
        _showHideItem.Click += (_, _) => ToggleWidgetVisible();

        _sessionItem = new ToolStripMenuItem("Session:  ↓ 0 B   ↑ 0 B") { Enabled = false };
        var resetSessionItem = new ToolStripMenuItem("Reset Session Counter");
        resetSessionItem.Click += (_, _) =>
        {
            _sessionDown = _sessionUp = 0;
            UpdateSessionText();
        };

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

        _clickThroughItem = new ToolStripMenuItem("Click-Through") { Checked = false };
        _clickThroughItem.Click += (_, _) =>
        {
            _settings.ClickThrough    = !_settings.ClickThrough;
            _clickThroughItem.Checked = _settings.ClickThrough;
            ApplyClickThrough(_settings.ClickThrough);
            _settings.Save();
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Dashboard…", null, (_, _) => OpenDashboard());
        menu.Items.Add("Usage History…", null, (_, _) => OpenUsageHistory());
        menu.Items.Add(_showHideItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_sessionItem);
        menu.Items.Add(resetSessionItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_alwaysOnTopItem);
        menu.Items.Add(_clickThroughItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_startupItem);
        menu.Items.Add("Settings…", null, (_, _) => OpenSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) =>
        {
            _trayIcon.Visible = false;
            Application.Exit();
        });

        ContextMenuStrip = menu;

        // ── Tray icon ────────────────────────────────────────────────────────

        _trayIcon = new NotifyIcon
        {
            Icon             = CreateTrayIcon(),
            Text             = "Speed Monitor",
            Visible          = true,
            ContextMenuStrip = menu,
        };
        _trayIcon.MouseDoubleClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) ToggleWidgetVisible();
        };

        // ── Drag ─────────────────────────────────────────────────────────────

        MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left || _settings.LockPosition) return;
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
        MouseUp += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            _dragging = false;
            if (_settings.SnapToEdges) SnapToNearestEdge();
            _settings.WindowX = Location.X;
            _settings.WindowY = Location.Y;
            _settings.Save();
            Render();
        };

        // ── Start ─────────────────────────────────────────────────────────────

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
        ApplyClickThrough(_settings.ClickThrough);
        Render();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible && IsHandleCreated) Render();
    }

    // ── Widget visibility ────────────────────────────────────────────────────

    private void ToggleWidgetVisible()
    {
        Visible               = !Visible;
        _showHideItem.Text    = Visible ? "Hide Widget" : "Show Widget";
    }

    // ── Click-through ────────────────────────────────────────────────────────

    private void ApplyClickThrough(bool on)
    {
        if (!IsHandleCreated) return;
        int ex = GetWindowLong(Handle, GWL_EXSTYLE);
        _ = SetWindowLong(Handle, GWL_EXSTYLE,
            on ? ex | WS_EX_TRANSPARENT : ex & ~WS_EX_TRANSPARENT);
    }

    // ── Snap to edges ──────────────────────────────────────────────────────────

    private const int SnapThreshold = 24;  // px — how close to an edge triggers a snap
    private const int SnapMargin     = 16;  // px — gap left between widget and edge

    private void SnapToNearestEdge()
    {
        var wa = Screen.FromPoint(Location).WorkingArea;

        int x = Location.X, y = Location.Y;

        if (Math.Abs(x - wa.Left) <= SnapThreshold)                  x = wa.Left + SnapMargin;
        else if (Math.Abs(wa.Right - (x + Width)) <= SnapThreshold)  x = wa.Right - Width - SnapMargin;

        if (Math.Abs(y - wa.Top) <= SnapThreshold)                   y = wa.Top + SnapMargin;
        else if (Math.Abs(wa.Bottom - (y + Height)) <= SnapThreshold) y = wa.Bottom - Height - SnapMargin;

        Location = new Point(x, y);
    }

    // ── Dashboard ──────────────────────────────────────────────────────────────

    private void OpenDashboard()
    {
        if (_dashboard is { IsDisposed: false })
        {
            _dashboard.WindowState = FormWindowState.Normal;
            _dashboard.Activate();
            return;
        }
        _dashboard = new DashboardForm(this);
        _dashboard.FormClosed += (_, _) => _dashboard = null;
        _dashboard.Show();
    }

    internal void OpenUsageHistory()
    {
        if (_historyForm is { IsDisposed: false })
        {
            _historyForm.WindowState = FormWindowState.Normal;
            _historyForm.Activate();
            return;
        }
        _historyForm = new UsageHistoryForm(this);
        _historyForm.FormClosed += (_, _) => _historyForm = null;
        _historyForm.Show();
    }

    /// <summary>Exports the full per-day usage history to a user-chosen CSV file.</summary>
    internal void ExportUsageCsv(IWin32Window parent)
    {
        try
        {
            using var dlg = new SaveFileDialog
            {
                Filter = "CSV file (*.csv)|*.csv",
                FileName = $"network-usage-{DateTime.Now:yyyy-MM-dd}.csv",
            };
            if (dlg.ShowDialog(parent) != DialogResult.OK) return;

            using var w = new StreamWriter(dlg.FileName);
            w.WriteLine("Date,DownloadBytes,UploadBytes");
            foreach (var (date, down, up) in _usage.OrderedDays().OrderBy(d => d.date))
                w.WriteLine($"{date:yyyy-MM-dd},{down},{up}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(parent, $"Export failed:\n{ex.Message}", "Export failed",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    // ── Settings ─────────────────────────────────────────────────────────────

    private void OpenSettings()
    {
        using var form = new SettingsForm(_settings, IsAutoStart());
        if (form.ShowDialog(this) != DialogResult.OK) return;
        if (form.AutoStartResult != IsAutoStart())
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key is not null)
            {
                if (form.AutoStartResult) key.SetValue(AppName, Application.ExecutablePath);
                else                      key.DeleteValue(AppName, false);
            }
            _startupItem.Checked = form.AutoStartResult;
        }
        _settings = form.Result;
        _settings.Save();
        _capWarned80 = _capWarned100 = false;  // re-arm cap warnings for the new cap
        ApplySettings(_settings, firstRun: false);
    }

    private void ApplySettings(AppSettings s, bool firstRun)
    {
        TopMost                   = s.AlwaysOnTop;
        _alwaysOnTopItem.Checked  = s.AlwaysOnTop;
        _clickThroughItem.Checked = s.ClickThrough;
        _font                     = new Font("Segoe UI", s.FontSize, FontStyle.Bold);
        _timer.Interval           = s.RefreshIntervalMs;

        var probe = s.DecimalUnits ? "↓  999.9 MB/s" : "↓  999.9 MiB/s";
        var sz    = TextRenderer.MeasureText(probe, _font);
        int lw    = sz.Width + 10;
        int lh    = sz.Height + 6;
        int spark = s.ShowSparkline ? SparkH + 8 : 0;

        bool showDown = s.ShowDownloadLine;
        bool showUp   = s.ShowUploadLine;
        if (!showDown && !showUp) { showDown = true; showUp = true; }

        // textH = height occupied by visible text rows (rows × lh, plus 4px gap between them)
        int textH = (showDown ? lh + 4 : 0) + (showUp ? lh + 4 : 0) - 4;

        if (s.CompactMode)
        {
            // Single tight row, both metrics side by side, minimal padding.
            _downRect  = new RectangleF(6, 4, lw, lh);
            _upRect    = new RectangleF(6 + lw, 4, lw, lh);
            ClientSize = new Size(2 * lw + 12, lh + 8 + spark);
            _sparkRect = new Rectangle(6, lh + 10, Width - 12, SparkH);
        }
        else if (s.Horizontal)
        {
            int x = 10;
            _downRect = showDown ? new RectangleF(x, 8, lw, lh) : RectangleF.Empty;
            if (showDown) x += lw + 8;
            _upRect   = showUp  ? new RectangleF(x, 8, lw, lh) : RectangleF.Empty;
            int cols  = (showDown ? 1 : 0) + (showUp ? 1 : 0);
            ClientSize = new Size(10 + cols * lw + (cols - 1) * 8 + 10, lh + 16 + spark);
            _sparkRect = new Rectangle(6, lh + 20, Width - 12, SparkH);
        }
        else
        {
            int y = 8;
            _downRect = showDown ? new RectangleF(10, y, lw, lh) : RectangleF.Empty;
            if (showDown) y += lh + 4;
            _upRect   = showUp  ? new RectangleF(10, y, lw, lh) : RectangleF.Empty;
            ClientSize = new Size(lw + 20, 8 + textH + 8 + spark);
            _sparkRect = new Rectangle(6, 8 + textH + 8 + 4, Width - 12, SparkH);
        }

        ApplyClickThrough(s.ClickThrough);

        if (firstRun)
        {
            bool onScreen = s.WindowX != int.MinValue
                && Screen.AllScreens.Any(sc => sc.WorkingArea.Contains(s.WindowX, s.WindowY));
            Location = onScreen
                ? new Point(s.WindowX, s.WindowY)
                : new Point(Screen.PrimaryScreen!.WorkingArea.Right  - Width  - 16,
                            Screen.PrimaryScreen!.WorkingArea.Bottom - Height - 16);
        }

        if (IsHandleCreated) Render();
    }

    // ── Rendering ────────────────────────────────────────────────────────────

    private void Render()
    {
        if (!IsHandleCreated || Width <= 0 || Height <= 0) return;

        int bgA   = Clamp255(_settings.BackgroundOpacity);
        int textA = Clamp255(_settings.TextOpacity);

        var downBase = Color.FromArgb(_settings.DownloadColor);
        var upBase   = Color.FromArgb(_settings.UploadColor);

        using var bmp = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            g.SmoothingMode     = SmoothingMode.AntiAlias;

            // Opaque background — text is antialiased against solid pixels;
            // opacity is applied per-pixel after drawing (see ApplyAlphaAndPremultiply).
            g.Clear(Color.FromArgb(18, 18, 18));

            using var borderPen = new Pen(Color.FromArgb(50, 50, 50));
            g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);

            using var downBrush = new SolidBrush(downBase);
            using var upBrush   = new SolidBrush(upBase);

            var fmt = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
            if (!_downRect.IsEmpty) g.DrawString(_downText, _font, downBrush, _downRect, fmt);
            if (!_upRect.IsEmpty)   g.DrawString(_upText,   _font, upBrush,   _upRect,   fmt);

            if (_settings.ShowSparkline)
                DrawSparkline(g, _sparkRect, downBase, upBase);
        }

        ApplyAlphaAndPremultiply(bmp, bgA, textA);
        BlitToScreen(bmp);
    }

    private void DrawSparkline(Graphics g, Rectangle area, Color downColor, Color upColor)
    {
        bool drawDown = _settings.ShowDownBars;
        bool drawUp   = _settings.ShowUpBars;
        if (!drawDown && !drawUp) return;

        // Each direction scales to its own peak so one spike can't crush the other.
        long maxDown = 1, maxUp = 1;
        for (int i = 0; i < HistLen; i++)
        {
            if (_downHist[i] > maxDown) maxDown = _downHist[i];
            if (_upHist[i]   > maxUp)   maxUp   = _upHist[i];
        }

        float barW = (float)area.Width / HistLen;

        var prevSmoothing = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.None;  // crisp pixel bars

        using var downBrush = new SolidBrush(downColor);
        using var upBrush   = new SolidBrush(upColor);

        if (drawDown && drawUp)
        {
            // Split layout: download on top half, upload on bottom half, 4px gap.
            int halfH      = (area.Height - 4) / 2;
            int downBottom = area.Top + halfH;
            int upTop      = area.Top + halfH + 4;

            for (int i = 0; i < HistLen; i++)
            {
                int idx = (_histIdx + i) % HistLen;
                int x   = area.Left + (int)(i * barW);
                int w   = Math.Max(1, (int)barW - 1);

                int dh = (int)((float)_downHist[idx] / maxDown * halfH);
                if (dh >= 1) g.FillRectangle(downBrush, x, downBottom - dh, w, dh);

                int uh = (int)((float)_upHist[idx] / maxUp * halfH);
                if (uh >= 1) g.FillRectangle(upBrush, x, upTop, w, uh);
            }
        }
        else
        {
            // Single bar type — expands to the full sparkline height.
            int fullH = area.Height;
            for (int i = 0; i < HistLen; i++)
            {
                int idx = (_histIdx + i) % HistLen;
                int x   = area.Left + (int)(i * barW);
                int w   = Math.Max(1, (int)barW - 1);

                if (drawDown)
                {
                    int dh = (int)((float)_downHist[idx] / maxDown * fullH);
                    if (dh >= 1) g.FillRectangle(downBrush, x, area.Bottom - dh, w, dh);
                }
                else
                {
                    int uh = (int)((float)_upHist[idx] / maxUp * fullH);
                    if (uh >= 1) g.FillRectangle(upBrush, x, area.Top, w, uh);
                }
            }
        }

        g.SmoothingMode = prevSmoothing;
    }

    private static int Clamp255(double v) => Math.Clamp((int)(v * 255), 0, 255);

    // Background pixels keep bgA; text/sparkline pixels keep textA.
    // Premultiplication is required by UpdateLayeredWindow with AC_SRC_ALPHA.
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
            int b = buf[i], gr = buf[i + 1], r = buf[i + 2];
            bool isBg = (r == 18 && gr == 18 && b == 18) ||  // background
                        (r == 50 && gr == 50 && b == 50);     // border
            int a = isBg ? bgA : textA;
            buf[i]     = (byte)(b  * a / 255);
            buf[i + 1] = (byte)(gr * a / 255);
            buf[i + 2] = (byte)(r  * a / 255);
            buf[i + 3] = (byte)a;
        }

        Marshal.Copy(buf, 0, data.Scan0, bytes);
        bmp.UnlockBits(data);
    }

    private void BlitToScreen(Bitmap bmp)
    {
        var screenDc = GetDC(IntPtr.Zero);
        var memDc    = CreateCompatibleDC(screenDc);
        var hBmp     = bmp.GetHbitmap(Color.FromArgb(0));
        var hOld     = SelectObject(memDc, hBmp);

        var blend = new BLENDFUNCTION { BlendOp = AC_SRC_OVER, AlphaFormat = AC_SRC_ALPHA, SourceConstantAlpha = 255 };
        var size  = new SIZE  { cx = Width, cy = Height };
        var srcPt = new POINT { X  = 0,     Y  = 0 };
        var dstPt = new POINT { X  = Left,  Y  = Top };

        UpdateLayeredWindow(Handle, screenDc, ref dstPt, ref size, memDc, ref srcPt, 0, ref blend, ULW_ALPHA);

        SelectObject(memDc, hOld);
        DeleteObject(hBmp);
        DeleteDC(memDc);
        ReleaseDC(IntPtr.Zero, screenDc);
    }

    // ── Network sampling ─────────────────────────────────────────────────────

    private void OnTick(object? sender, EventArgs e)
    {
        var (received, sent) = NetworkStats.GetTotals(_settings.AdapterName);
        var now     = DateTime.UtcNow;
        var elapsed = (now - _lastSampleTime).TotalSeconds;
        long down = 0, up = 0;
        long downBytes = Math.Max(0, received - _lastReceived);
        long upBytes   = Math.Max(0, sent - _lastSent);
        if (elapsed > 0)
        {
            down = (long)(downBytes / elapsed);
            up   = (long)(upBytes / elapsed);
        }
        _lastReceived   = received;
        _lastSent       = sent;
        _lastSampleTime = now;

        _curDown = down;
        _curUp   = up;

        _sessionDown += downBytes;
        _sessionUp   += upBytes;

        _usage.Add(downBytes, upBytes);
        if (++_ticksSinceUsageSave >= 30) { _usage.Save(); _ticksSinceUsageSave = 0; }

        CheckMonthlyCap();

        _downHist[_histIdx] = down;
        _upHist[_histIdx]   = up;
        _histIdx = (_histIdx + 1) % HistLen;

        UpdateSpeedText(down, up);
        UpdateSessionText();

        if (_settings.PingEnabled)
        {
            _ping.Host = _settings.PingHost;
            _ = _ping.PingOnceAsync();
            CheckConnectionState();
        }

        _dashboard?.RefreshData();

        var tip = $"↓ {FormatSpeed(down)}  ↑ {FormatSpeed(up)}\nSession: ↓ {FormatBytes(_sessionDown)}  ↑ {FormatBytes(_sessionUp)}";
        _trayIcon.Text = tip.Length > 127 ? tip[..127] : tip;
    }

    // Live speed / session accessors for the dashboard.
    internal (long down, long up) CurrentSpeed  => (_curDown, _curUp);
    internal (long down, long up) SessionTotals => (_sessionDown, _sessionUp);
    internal UsageTracker Usage => _usage;
    internal PingMonitor  Ping  => _ping;
    internal AppSettings  Settings => _settings;

    internal void ResetSession()
    {
        _sessionDown = _sessionUp = 0;
        UpdateSessionText();
    }

    private void CheckConnectionState()
    {
        bool up = _ping.IsUp;
        if (_wasUp && !up)
            _trayIcon.ShowBalloonTip(3000, "Speed Monitor",
                $"Connection lost — no reply from {_settings.PingHost}", ToolTipIcon.Warning);
        _wasUp = up;
    }

    private void CheckMonthlyCap()
    {
        if (_settings.MonthlyCapBytes <= 0) return;
        var (md, mu) = _usage.Month;
        double pct = (md + mu) * 100.0 / _settings.MonthlyCapBytes;

        if (pct >= 100 && !_capWarned100)
        {
            _capWarned100 = true;
            _trayIcon.ShowBalloonTip(5000, "Data cap reached",
                $"You've used 100% of your {FormatBytes(_settings.MonthlyCapBytes)} monthly cap.",
                ToolTipIcon.Warning);
        }
        else if (pct >= 80 && !_capWarned80)
        {
            _capWarned80 = true;
            _trayIcon.ShowBalloonTip(5000, "Data cap warning",
                $"You've used 80% of your {FormatBytes(_settings.MonthlyCapBytes)} monthly cap.",
                ToolTipIcon.Info);
        }
    }

    private void UpdateSessionText() =>
        _sessionItem.Text = $"Session:  ↓ {FormatBytes(_sessionDown)}   ↑ {FormatBytes(_sessionUp)}";

    private string FormatBytes(long bytes) => Format.Bytes(bytes, _settings.DecimalUnits);

    private void UpdateSpeedText(long downBps, long upBps)
    {
        _downText = $"↓  {FormatSpeed(downBps)}";
        _upText   = $"↑  {FormatSpeed(upBps)}";
        if (IsHandleCreated) Render();
    }

    private string FormatSpeed(long bps) =>
        _settings.ShowBits ? Format.SpeedBits(bps) : Format.Speed(bps, _settings.DecimalUnits);

    // ── Tray icon ────────────────────────────────────────────────────────────

    private static Icon CreateTrayIcon()
    {
        using var bmp = new Bitmap(16, 16, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            // Download arrow (green, left side)
            using var dBrush = new SolidBrush(Color.FromArgb(60, 220, 60));
            g.FillPolygon(dBrush, (PointF[])[new(1,3), new(7,3), new(4,10)]);

            // Upload arrow (orange, right side)
            using var uBrush = new SolidBrush(Color.FromArgb(255, 160, 30));
            g.FillPolygon(uBrush, (PointF[])[new(9,12), new(15,12), new(12,5)]);
        }
        var hIcon = bmp.GetHicon();
        var icon  = (Icon)Icon.FromHandle(hIcon).Clone();
        DestroyIcon(hIcon);
        return icon;
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
        if (disposing)
        {
            _usage.Save();
            _timer.Dispose();
            _font.Dispose();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}
