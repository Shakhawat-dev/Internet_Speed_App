using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace InternetSpeedApp;

internal sealed class SpeedWindow : Form
{
    private const string AppName = "InternetSpeedApp";
    private const string RunKey  = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    private readonly Label  _downLabel;
    private readonly Label  _upLabel;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly ToolStripMenuItem _startupItem;

    private AppSettings _settings;
    private long  _lastReceived, _lastSent;
    private DateTime _lastSampleTime;

    private bool  _dragging;
    private Point _mouseDownScreen, _locationAtMouseDown;

    internal SpeedWindow()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition   = FormStartPosition.Manual;
        TopMost         = true;
        ShowInTaskbar   = false;
        BackColor       = Color.FromArgb(18, 18, 18);

        _downLabel = MakeLabel(Color.FromArgb(60, 220, 60));
        _upLabel   = MakeLabel(Color.FromArgb(255, 160, 30));
        Controls.AddRange([_downLabel, _upLabel]);

        _startupItem = new ToolStripMenuItem("Start with Windows") { Checked = IsAutoStart() };
        _startupItem.Click += (_, _) => ToggleAutoStart(_startupItem);

        var menu = new ContextMenuStrip();
        menu.Items.Add(_startupItem);
        menu.Items.Add("Settings…", null, (_, _) => OpenSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Application.Exit());

        ContextMenuStrip    = menu;
        _downLabel.ContextMenuStrip = menu;
        _upLabel.ContextMenuStrip   = menu;

        AttachDrag(this);
        AttachDrag(_downLabel);
        AttachDrag(_upLabel);

        (_lastReceived, _lastSent) = NetworkStats.GetTotals();
        _lastSampleTime = DateTime.UtcNow;

        _timer = new System.Windows.Forms.Timer { Interval = 1000 };
        _timer.Tick += OnTick;
        _timer.Start();

        _settings = AppSettings.Load();
        ApplySettings(_settings, firstRun: true);
        UpdateLabels(0, 0);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int pref = DWMWCP_ROUND;
        DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));
    }

    private static Label MakeLabel(Color color) => new()
    {
        ForeColor = color,
        AutoSize  = false,
        TextAlign = ContentAlignment.MiddleLeft,
        BackColor = Color.Transparent,
    };

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
        Opacity = s.Opacity;

        var font = new Font("Segoe UI", s.FontSize, FontStyle.Bold);
        _downLabel.Font = font;
        _upLabel.Font   = font;

        // Measure the widest text that will ever appear at this font size
        var probe = s.DecimalUnits ? "↓  999.9 MB/s" : "↓  999.9 MiB/s";
        var sz = TextRenderer.MeasureText(probe, font);
        int lw = sz.Width + 10;
        int lh = sz.Height + 6;

        if (s.Horizontal)
        {
            _downLabel.Location = new Point(10, 8);
            _downLabel.Size     = new Size(lw, lh);
            _upLabel.Location   = new Point(10 + lw + 8, 8);
            _upLabel.Size       = new Size(lw, lh);
            ClientSize          = new Size(10 + lw * 2 + 8 + 10, lh + 16);
        }
        else
        {
            _downLabel.Location = new Point(10, 8);
            _downLabel.Size     = new Size(lw, lh);
            _upLabel.Location   = new Point(10, 8 + lh + 4);
            _upLabel.Size       = new Size(lw, lh);
            ClientSize          = new Size(lw + 20, 8 + lh * 2 + 4 + 8);
        }

        if (firstRun)
        {
            var wa = Screen.PrimaryScreen!.WorkingArea;
            Location = new Point(wa.Right - Width - 16, wa.Bottom - Height - 16);
        }
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
        UpdateLabels(down, up);
    }

    private void UpdateLabels(long downBps, long upBps)
    {
        _downLabel.Text = $"↓  {FormatSpeed(downBps)}";
        _upLabel.Text   = $"↑  {FormatSpeed(upBps)}";
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

    // ── Drag ─────────────────────────────────────────────────────────────────

    private void AttachDrag(Control c)
    {
        c.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            _dragging             = true;
            _mouseDownScreen      = MousePosition;
            _locationAtMouseDown  = Location;
        };
        c.MouseMove += (_, _) =>
        {
            if (!_dragging) return;
            Location = new Point(
                _locationAtMouseDown.X + MousePosition.X - _mouseDownScreen.X,
                _locationAtMouseDown.Y + MousePosition.Y - _mouseDownScreen.Y);
        };
        c.MouseUp += (_, e) => { if (e.Button == MouseButtons.Left) _dragging = false; };
    }

    // ── Paint ────────────────────────────────────────────────────────────────

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(Color.FromArgb(50, 50, 50));
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }

    // ── Win32 hints ──────────────────────────────────────────────────────────

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
            cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW (hide from Alt+Tab)
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
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }
}
