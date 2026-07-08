using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace InternetSpeedApp;

/// <summary>
/// A normal window surfacing everything the tiny widget can't: live speeds,
/// latency, session/today/month usage, a monthly-cap gauge, a per-day history
/// chart, and network info. Refreshed each second by the owning
/// <see cref="SpeedWindow"/> via <see cref="RefreshData"/>.
/// </summary>
internal sealed class DashboardForm : Form
{
    private readonly SpeedWindow _owner;

    private static readonly Color Bg      = Color.FromArgb(24, 24, 24);
    private static readonly Color Panel   = Color.FromArgb(34, 34, 34);
    private static readonly Color TextDim = Color.FromArgb(160, 160, 160);

    private readonly Label _downSpeed, _upSpeed, _ping;
    private readonly Label _session, _today, _month;
    private readonly Label _capLabel;
    private readonly ProgressBar _capBar;
    private readonly Label _ipLabel, _ssidLabel, _adapterLabel;
    private readonly HistoryChart _chart;

    private int _infoCounter;  // throttles the (slow) Wi-Fi/IP lookups

    internal DashboardForm(SpeedWindow owner)
    {
        _owner = owner;

        Text            = "Speed Monitor — Dashboard";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox     = false;
        StartPosition   = FormStartPosition.CenterScreen;
        ClientSize      = new Size(460, 616);
        BackColor       = Bg;
        ForeColor       = Color.White;
        ShowInTaskbar   = true;
        Font            = new Font("Segoe UI", 9f);

        // ── Live ──────────────────────────────────────────────────────────────
        var liveBox = MakePanel(16, 16, 428, 92, "Live");
        _downSpeed = MakeBig(liveBox, 16, 34);
        _upSpeed   = MakeBig(liveBox, 160, 34);
        _ping      = MakeBig(liveBox, 304, 34);
        MakeCaption(liveBox, "Download", 16, 66);
        MakeCaption(liveBox, "Upload",   160, 66);
        MakeCaption(liveBox, "Ping",     304, 66);
        Controls.Add(liveBox);

        // ── Usage ─────────────────────────────────────────────────────────────
        var usageBox = MakePanel(16, 120, 428, 112, "Data usage");
        _session = MakeRow(usageBox, "This session", 40);
        _today   = MakeRow(usageBox, "Today",        64);
        _month   = MakeRow(usageBox, "This month",   88);
        Controls.Add(usageBox);

        // ── Monthly cap ─────────────────────────────────────────────────────────
        var capBox = MakePanel(16, 244, 428, 78, "Monthly cap");
        _capBar = new ProgressBar
        {
            Location = new Point(16, 34), Size = new Size(396, 18),
            Minimum = 0, Maximum = 100, Style = ProgressBarStyle.Continuous,
        };
        _capLabel = new Label
        {
            Location = new Point(16, 56), AutoSize = true, ForeColor = TextDim,
        };
        capBox.Controls.Add(_capBar);
        capBox.Controls.Add(_capLabel);
        Controls.Add(capBox);

        // ── History chart ─────────────────────────────────────────────────────
        var chartBox = MakePanel(16, 334, 428, 148, "Last 30 days");
        _chart = new HistoryChart(owner)
        {
            Location = new Point(12, 30), Size = new Size(404, 106),
            BackColor = Panel,
        };
        chartBox.Controls.Add(_chart);
        Controls.Add(chartBox);

        // ── Network info ─────────────────────────────────────────────────────
        var infoBox = MakePanel(16, 494, 428, 78, "Network");
        _ipLabel      = MakeInfo(infoBox, "Local IP", 30);
        _ssidLabel    = MakeInfo(infoBox, "Wi-Fi",    50);
        _adapterLabel = MakeInfo(infoBox, "Adapter",  70);
        Controls.Add(infoBox);

        // ── Buttons ───────────────────────────────────────────────────────────
        var resetBtn = MakeButton("Reset Session", 16, 582);
        resetBtn.Click += (_, _) => { _owner.ResetSession(); RefreshData(); };
        var exportBtn = MakeButton("Export CSV…", 152, 582);
        exportBtn.Click += (_, _) => ExportCsv();
        var closeBtn = MakeButton("Close", 370, 582);
        closeBtn.Click += (_, _) => Close();
        Controls.AddRange([resetBtn, exportBtn, closeBtn]);

        RefreshData();
    }

    /// <summary>Called by the owner each tick to refresh all displayed values.</summary>
    internal void RefreshData()
    {
        if (IsDisposed || !IsHandleCreated) return;

        var s       = _owner.Settings;
        var (cd, cu) = _owner.CurrentSpeed;
        var (sd, su) = _owner.SessionTotals;
        var (td, tu) = _owner.Usage.Today;
        var (md, mu) = _owner.Usage.Month;

        _downSpeed.Text = "↓ " + FmtSpeed(cd, s);
        _downSpeed.ForeColor = Color.FromArgb(s.DownloadColor);
        _upSpeed.Text   = "↑ " + FmtSpeed(cu, s);
        _upSpeed.ForeColor = Color.FromArgb(s.UploadColor);

        if (!s.PingEnabled)      { _ping.Text = "—";  _ping.ForeColor = TextDim; }
        else if (_owner.Ping.IsUp)
        {
            int ms = _owner.Ping.LatestMs;
            _ping.Text = $"{ms} ms";
            _ping.ForeColor = ms < 60 ? Color.FromArgb(60, 220, 60)
                            : ms < 150 ? Color.FromArgb(230, 200, 40)
                            : Color.FromArgb(230, 80, 60);
        }
        else { _ping.Text = "down"; _ping.ForeColor = Color.FromArgb(230, 80, 60); }

        _session.Text = FmtPair(sd, su, s.DecimalUnits);
        _today.Text   = FmtPair(td, tu, s.DecimalUnits);
        _month.Text   = FmtPair(md, mu, s.DecimalUnits);

        UpdateCap(md + mu, s);

        if (_infoCounter-- <= 0) { RefreshNetworkInfo(); _infoCounter = 30; }

        _chart.Invalidate();
    }

    private void UpdateCap(long monthTotal, AppSettings s)
    {
        if (s.MonthlyCapBytes <= 0)
        {
            _capBar.Value  = 0;
            _capLabel.Text = "No cap set (configure in Settings).";
            return;
        }
        int pct = (int)Math.Clamp(monthTotal * 100.0 / s.MonthlyCapBytes, 0, 100);
        _capBar.Value  = pct;
        _capLabel.Text =
            $"{Format.Bytes(monthTotal, s.DecimalUnits)} of {Format.Bytes(s.MonthlyCapBytes, s.DecimalUnits)}  ({pct}%)";
        _capLabel.ForeColor = pct >= 100 ? Color.FromArgb(230, 80, 60)
                            : pct >= 80  ? Color.FromArgb(230, 200, 40)
                            : TextDim;
    }

    private void RefreshNetworkInfo()
    {
        var s = _owner.Settings;
        _ipLabel.Text      = "Local IP:  " + NetworkStats.GetLocalIPv4(s.AdapterName);
        string ssid = NetworkStats.GetWifiSsid();
        _ssidLabel.Text    = "Wi-Fi:  " + (string.IsNullOrEmpty(ssid) ? "not connected" : ssid);
        _adapterLabel.Text = "Adapter:  " + (string.IsNullOrEmpty(s.AdapterName) ? "All adapters" : s.AdapterName);
    }

    private void ExportCsv()
    {
        using var dlg = new SaveFileDialog
        {
            Filter = "CSV file (*.csv)|*.csv",
            FileName = $"network-usage-{DateTime.Now:yyyy-MM-dd}.csv",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            using var w = new StreamWriter(dlg.FileName);
            w.WriteLine("Date,DownloadBytes,UploadBytes");
            foreach (var (date, down, up) in _owner.Usage.OrderedDays().OrderBy(d => d.date))
                w.WriteLine($"{date:yyyy-MM-dd},{down},{up}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not write file:\n{ex.Message}", "Export failed",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static string FmtSpeed(long bps, AppSettings s) =>
        s.ShowBits ? Format.SpeedBits(bps) : Format.Speed(bps, s.DecimalUnits);

    private static string FmtPair(long down, long up, bool dec) =>
        $"↓ {Format.Bytes(down, dec)}      ↑ {Format.Bytes(up, dec)}";

    // ── Control factories ─────────────────────────────────────────────────────

    private static Panel MakePanel(int x, int y, int w, int h, string title)
    {
        var p = new Panel { Location = new Point(x, y), Size = new Size(w, h), BackColor = Panel };
        p.Controls.Add(new Label
        {
            Text = title, Location = new Point(12, 8), AutoSize = true,
            ForeColor = TextDim, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
        });
        return p;
    }

    private static Label MakeBig(Control parent, int x, int y)
    {
        var l = new Label
        {
            Location = new Point(x, y), AutoSize = true,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold), Text = "—",
        };
        parent.Controls.Add(l);
        return l;
    }

    private static void MakeCaption(Control parent, string text, int x, int y) =>
        parent.Controls.Add(new Label
        {
            Text = text, Location = new Point(x, y), AutoSize = true, ForeColor = TextDim,
        });

    private static Label MakeRow(Control parent, string caption, int y)
    {
        parent.Controls.Add(new Label
        {
            Text = caption, Location = new Point(16, y), Size = new Size(96, 20), ForeColor = TextDim,
        });
        var val = new Label { Location = new Point(120, y), AutoSize = true, Text = "—" };
        parent.Controls.Add(val);
        return val;
    }

    private static Label MakeInfo(Control parent, string caption, int y)
    {
        var l = new Label { Location = new Point(16, y), AutoSize = true, Text = caption + ":  —" };
        parent.Controls.Add(l);
        return l;
    }

    private Button MakeButton(string text, int x, int y) => new()
    {
        Text = text, Location = new Point(x, y), Size = new Size(84, 26),
        FlatStyle = FlatStyle.Flat, ForeColor = Color.White,
        BackColor = Color.FromArgb(48, 48, 48),
        FlatAppearance = { BorderColor = Color.FromArgb(80, 80, 80) },
    };

    // ── Daily history chart ─────────────────────────────────────────────────────

    private sealed class HistoryChart(SpeedWindow owner) : Panel
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.None;

            var days = owner.Usage.OrderedDays()
                .OrderBy(d => d.date)
                .TakeLast(30)
                .ToList();
            if (days.Count == 0)
            {
                TextRenderer.DrawText(g, "No data yet", Font, ClientRectangle,
                    TextDim, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            long max = 1;
            foreach (var (_, down, up) in days)
                max = Math.Max(max, down + up);

            var s = owner.Settings;
            using var downBrush = new SolidBrush(Color.FromArgb(s.DownloadColor));
            using var upBrush   = new SolidBrush(Color.FromArgb(s.UploadColor));

            int n = days.Count;
            float slot = (float)Width / n;
            float barW = Math.Max(2, slot - 2);

            for (int i = 0; i < n; i++)
            {
                var (_, down, up) = days[i];
                float x = i * slot + (slot - barW) / 2;
                int dh = (int)((float)down / max * (Height - 2));
                int uh = (int)((float)up   / max * (Height - 2));
                if (uh >= 1) g.FillRectangle(upBrush,   x, Height - uh, barW, uh);
                if (dh >= 1) g.FillRectangle(downBrush, x, Height - uh - dh, barW, dh);
            }
        }
    }
}
