using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using InternetSpeedApp.Core;
using InternetSpeedApp.Interop;

namespace InternetSpeedApp.UI;

/// <summary>
/// The floating widget and application shell: owns the sample timer, tray icon,
/// context menu, and child windows (dashboard, history, settings). Measurement
/// lives in <see cref="SpeedSampler"/>/<see cref="UsageTracker"/>; presentation
/// plumbing in <see cref="LayeredWindowPresenter"/>.
/// </summary>
internal sealed class SpeedWindow : Form
{
    private const int SparkH        = 36;  // total height of the sparkline area
    private const int SnapThreshold = 24;  // px from an edge that triggers a snap
    private const int SnapMargin    = 16;  // px gap left between widget and edge

    // Where "Support the Project" sends people.
    private const string DonateUrl = "https://ko-fi.com/shakhawat_dev";

    // ── Services ─────────────────────────────────────────────────────────────

    private readonly SpeedSampler _sampler = new();
    private readonly UsageTracker _usage   = UsageTracker.Load();
    private readonly PingMonitor  _ping    = new();
    private AppSettings           _settings = AppSettings.Load();

    // ── UI members ───────────────────────────────────────────────────────────

    private readonly System.Windows.Forms.Timer _timer;
    private readonly ToolStripMenuItem _startupItem;
    private readonly ToolStripMenuItem _alwaysOnTopItem;
    private readonly ToolStripMenuItem _clickThroughItem;
    private readonly ToolStripMenuItem _showHideItem;
    private readonly ToolStripMenuItem _sessionItem;
    private readonly NotifyIcon        _trayIcon;

    private DashboardForm?    _dashboard;
    private UsageHistoryForm? _historyForm;
    private TaskbarIndicator? _taskbarIndicator;
    private readonly ToolStripMenuItem _taskbarItem;

    // ── Widget layout / paint state ──────────────────────────────────────────

    private Font       _font = new("Segoe UI", 13f, FontStyle.Bold);
    private RectangleF _downRect, _upRect;
    private Rectangle  _sparkRect;
    private string     _downText = "↓  —";
    private string     _upText   = "↑  —";

    private bool  _dragging;
    private Point _mouseDownScreen, _locationAtMouseDown;

    // ── Alerting state ───────────────────────────────────────────────────────

    private int  _ticksSinceUsageSave;
    private bool _wasUp = true;                    // connection-lost alert edge
    private bool _capWarned80, _capWarned100;      // fired once each per session

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
        resetSessionItem.Click += (_, _) => ResetSession();

        _startupItem = new ToolStripMenuItem("Start with Windows") { Checked = AutoStart.IsEnabled };
        _startupItem.Click += (_, _) =>
        {
            bool enable = !_startupItem.Checked;
            AutoStart.SetEnabled(enable, Application.ExecutablePath);
            _startupItem.Checked = enable;
        };

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
            if (IsHandleCreated) NativeMethods.SetClickThrough(Handle, _settings.ClickThrough);
            _settings.Save();
        };

        _taskbarItem = new ToolStripMenuItem("Taskbar Indicator") { Checked = false };
        _taskbarItem.Click += (_, _) =>
        {
            _settings.ShowTaskbarIndicator = !_settings.ShowTaskbarIndicator;
            _taskbarItem.Checked           = _settings.ShowTaskbarIndicator;
            ApplyTaskbarIndicator(_settings.ShowTaskbarIndicator);
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
        menu.Items.Add(_taskbarItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_startupItem);
        menu.Items.Add("Settings…", null, (_, _) => OpenSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Support the Project ❤", null, (_, _) => OpenDonatePage());
        menu.Items.Add("Exit", null, (_, _) => ExitApp());

        ContextMenuStrip = menu;

        // ── Tray icon ────────────────────────────────────────────────────────

        _trayIcon = new NotifyIcon
        {
            Icon             = TrayIconFactory.Create(),
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

        _timer = new System.Windows.Forms.Timer { Interval = 1000 };
        _timer.Tick += OnTick;
        _timer.Start();

        ApplySettings(_settings, firstRun: true);
        UpdateSpeedText(0, 0);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        NativeMethods.ApplyRoundedCorners(Handle);
        NativeMethods.SetClickThrough(Handle, _settings.ClickThrough);
        Render();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible && IsHandleCreated) Render();
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

    // ── Accessors for child windows ──────────────────────────────────────────

    internal (long down, long up) CurrentSpeed  => (_sampler.CurrentDown, _sampler.CurrentUp);
    internal (long down, long up) SessionTotals => (_sampler.SessionDown, _sampler.SessionUp);
    internal UsageTracker Usage    => _usage;
    internal PingMonitor  Ping     => _ping;
    internal AppSettings  Settings => _settings;

    internal void ResetSession()
    {
        _sampler.ResetSession();
        UpdateSessionText();
    }

    internal void SaveTaskbarIndicatorGap(int gap)
    {
        _settings.TaskbarIndicatorGap = gap;
        _settings.Save();
    }

    // ── Sampling tick ────────────────────────────────────────────────────────

    private void OnTick(object? sender, EventArgs e)
    {
        var (downBytes, upBytes) = _sampler.Sample(_settings.AdapterName);

        _usage.Add(downBytes, upBytes);
        if (++_ticksSinceUsageSave >= 30) { _usage.Save(); _ticksSinceUsageSave = 0; }

        CheckMonthlyCap();

        UpdateSpeedText(_sampler.CurrentDown, _sampler.CurrentUp);
        UpdateSessionText();

        if (_settings.PingEnabled)
        {
            _ping.Host = _settings.PingHost;
            _ = _ping.PingOnceAsync();
            CheckConnectionState();
        }

        _dashboard?.RefreshData();
        _taskbarIndicator?.UpdateSpeeds(
            $"↑: {FormatSpeed(_sampler.CurrentUp)}",
            $"↓: {FormatSpeed(_sampler.CurrentDown)}");

        var tip = $"↓ {FormatSpeed(_sampler.CurrentDown)}  ↑ {FormatSpeed(_sampler.CurrentUp)}\n"
                + $"Session: ↓ {FormatBytes(_sampler.SessionDown)}  ↑ {FormatBytes(_sampler.SessionUp)}";
        _trayIcon.Text = tip.Length > 127 ? tip[..127] : tip;
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

    // ── Child windows ────────────────────────────────────────────────────────

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

    private void OpenSettings()
    {
        using var form = new SettingsForm(_settings, AutoStart.IsEnabled);
        if (form.ShowDialog(this) != DialogResult.OK) return;

        if (form.AutoStartResult != AutoStart.IsEnabled)
        {
            AutoStart.SetEnabled(form.AutoStartResult, Application.ExecutablePath);
            _startupItem.Checked = form.AutoStartResult;
        }
        _settings = form.Result;
        _settings.Save();
        _capWarned80 = _capWarned100 = false;  // re-arm cap warnings for the new cap
        ApplySettings(_settings, firstRun: false);
    }

    // ── Widget behavior ──────────────────────────────────────────────────────

    internal void ToggleWidgetVisible()
    {
        Visible            = !Visible;
        _showHideItem.Text = Visible ? "Hide Widget" : "Show Widget";
    }

    private void ApplyTaskbarIndicator(bool show)
    {
        if (show && _taskbarIndicator is null or { IsDisposed: true })
        {
            _taskbarIndicator = new TaskbarIndicator(this, ContextMenuStrip!);
            _taskbarIndicator.ShowInsideTaskbar();
        }
        else if (!show && _taskbarIndicator is { IsDisposed: false })
        {
            _taskbarIndicator.Close();
            _taskbarIndicator.Dispose();
            _taskbarIndicator = null;
        }
    }

    private void ExitApp()
    {
        _trayIcon.Visible = false;
        Application.Exit();
    }

    private static void OpenDonatePage()
    {
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(DonateUrl) { UseShellExecute = true });
        }
        catch { /* no browser available — nothing sensible to do */ }
    }

    private void SnapToNearestEdge()
    {
        var wa = Screen.FromPoint(Location).WorkingArea;

        int x = Location.X, y = Location.Y;

        if (Math.Abs(x - wa.Left) <= SnapThreshold)                   x = wa.Left + SnapMargin;
        else if (Math.Abs(wa.Right - (x + Width)) <= SnapThreshold)   x = wa.Right - Width - SnapMargin;

        if (Math.Abs(y - wa.Top) <= SnapThreshold)                    y = wa.Top + SnapMargin;
        else if (Math.Abs(wa.Bottom - (y + Height)) <= SnapThreshold) y = wa.Bottom - Height - SnapMargin;

        Location = new Point(x, y);
    }

    // ── Layout ───────────────────────────────────────────────────────────────

    private void ApplySettings(AppSettings s, bool firstRun)
    {
        TopMost                   = s.AlwaysOnTop;
        _alwaysOnTopItem.Checked  = s.AlwaysOnTop;
        _clickThroughItem.Checked = s.ClickThrough;
        _taskbarItem.Checked      = s.ShowTaskbarIndicator;
        _font                     = new Font("Segoe UI", s.FontSize, FontStyle.Bold);
        _timer.Interval           = s.RefreshIntervalMs;

        ApplyTaskbarIndicator(s.ShowTaskbarIndicator);

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

        if (IsHandleCreated) NativeMethods.SetClickThrough(Handle, s.ClickThrough);

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

            // Opaque background — text antialiases against solid pixels; the
            // presenter applies the user's opacities per pixel afterwards.
            g.Clear(LayeredWindowPresenter.BackgroundColor);

            using var borderPen = new Pen(LayeredWindowPresenter.BorderColor);
            g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);

            using var downBrush = new SolidBrush(downBase);
            using var upBrush   = new SolidBrush(upBase);

            var fmt = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
            if (!_downRect.IsEmpty) g.DrawString(_downText, _font, downBrush, _downRect, fmt);
            if (!_upRect.IsEmpty)   g.DrawString(_upText,   _font, upBrush,   _upRect,   fmt);

            if (_settings.ShowSparkline)
                DrawSparkline(g, _sparkRect, downBase, upBase);
        }

        LayeredWindowPresenter.Present(Handle, bmp, Location, bgA, textA);
    }

    private void DrawSparkline(Graphics g, Rectangle area, Color downColor, Color upColor)
    {
        bool drawDown = _settings.ShowDownBars;
        bool drawUp   = _settings.ShowUpBars;
        if (!drawDown && !drawUp) return;

        var downHist = _sampler.DownHistory;
        var upHist   = _sampler.UpHistory;
        int histLen  = SpeedSampler.HistoryLength;
        int histIdx  = _sampler.HistoryIndex;

        // Each direction scales to its own peak so one spike can't crush the other.
        long maxDown = 1, maxUp = 1;
        for (int i = 0; i < histLen; i++)
        {
            if (downHist[i] > maxDown) maxDown = downHist[i];
            if (upHist[i]   > maxUp)   maxUp   = upHist[i];
        }

        float barW = (float)area.Width / histLen;

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

            for (int i = 0; i < histLen; i++)
            {
                int idx = (histIdx + i) % histLen;   // oldest → newest, left to right
                int x   = area.Left + (int)(i * barW);
                int w   = Math.Max(1, (int)barW - 1);

                int dh = (int)((float)downHist[idx] / maxDown * halfH);
                if (dh >= 1) g.FillRectangle(downBrush, x, downBottom - dh, w, dh);

                int uh = (int)((float)upHist[idx] / maxUp * halfH);
                if (uh >= 1) g.FillRectangle(upBrush, x, upTop, w, uh);
            }
        }
        else
        {
            // Single bar type — expands to the full sparkline height.
            int fullH = area.Height;
            for (int i = 0; i < histLen; i++)
            {
                int idx = (histIdx + i) % histLen;
                int x   = area.Left + (int)(i * barW);
                int w   = Math.Max(1, (int)barW - 1);

                if (drawDown)
                {
                    int dh = (int)((float)downHist[idx] / maxDown * fullH);
                    if (dh >= 1) g.FillRectangle(downBrush, x, area.Bottom - dh, w, dh);
                }
                else
                {
                    int uh = (int)((float)upHist[idx] / maxUp * fullH);
                    if (uh >= 1) g.FillRectangle(upBrush, x, area.Top, w, uh);
                }
            }
        }

        g.SmoothingMode = prevSmoothing;
    }

    private static int Clamp255(double v) => Math.Clamp((int)(v * 255), 0, 255);

    // ── Text ─────────────────────────────────────────────────────────────────

    private void UpdateSpeedText(long downBps, long upBps)
    {
        _downText = $"↓  {FormatSpeed(downBps)}";
        _upText   = $"↑  {FormatSpeed(upBps)}";
        if (IsHandleCreated) Render();
    }

    private void UpdateSessionText() =>
        _sessionItem.Text = $"Session:  ↓ {FormatBytes(_sampler.SessionDown)}   ↑ {FormatBytes(_sampler.SessionUp)}";

    private string FormatSpeed(long bps) =>
        _settings.ShowBits ? Format.SpeedBits(bps) : Format.Speed(bps, _settings.DecimalUnits);

    private string FormatBytes(long bytes) => Format.Bytes(bytes, _settings.DecimalUnits);

    // ── Teardown ─────────────────────────────────────────────────────────────

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _usage.Save();
            _timer.Dispose();
            _font.Dispose();
            _taskbarIndicator?.Dispose();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}
