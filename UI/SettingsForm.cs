using System.Drawing;
using System.Windows.Forms;
using InternetSpeedApp.Core;

namespace InternetSpeedApp.UI;

internal sealed class SettingsForm : Form
{
    // Appearance
    private readonly TrackBar      _bgOpacityBar;
    private readonly Label         _bgOpacityValueLabel;
    private readonly TrackBar      _textOpacityBar;
    private readonly Label         _textOpacityValueLabel;
    private readonly NumericUpDown _fontSpinner;
    private readonly Button        _downColorBtn;
    private readonly Button        _upColorBtn;
    private readonly CheckBox      _sparklineCheck;
    private readonly CheckBox      _showDownBarsCheck;
    private readonly CheckBox      _showUpBarsCheck;
    private readonly CheckBox      _showDownLineCheck;
    private readonly CheckBox      _showUpLineCheck;
    // Layout & Units
    private readonly RadioButton   _verticalRadio;
    private readonly RadioButton   _horizontalRadio;
    private readonly CheckBox      _compactCheck;
    private readonly RadioButton   _binaryRadio;
    private readonly RadioButton   _decimalRadio;
    private readonly CheckBox      _bitsCheck;
    // Window
    private readonly CheckBox      _alwaysOnTopCheck;
    private readonly CheckBox      _snapCheck;
    private readonly CheckBox      _lockCheck;
    private readonly CheckBox      _clickThroughCheck;
    private readonly CheckBox      _taskbarCheck;
    private readonly CheckBox      _startWithWindowsCheck;
    private readonly ComboBox      _refreshCombo;
    // Network & Data
    private readonly ComboBox      _adapterCombo;
    private readonly CheckBox      _pingEnabledCheck;
    private readonly TextBox       _pingHostText;
    private readonly NumericUpDown _capSpinner;

    private const long GB = 1_000_000_000;

    internal bool AutoStartResult { get; private set; }
    internal AppSettings Result { get; private set; }

    private readonly AppSettings _current;

    internal SettingsForm(AppSettings current, bool autoStart)
    {
        Result   = current;
        _current = current;

        Text            = "Speed Monitor — Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition   = FormStartPosition.CenterScreen;
        ClientSize      = new Size(384, 452);
        MaximizeBox     = false;
        MinimizeBox     = false;
        ShowInTaskbar   = false;

        var tabs = new TabControl { Location = new Point(8, 8), Size = new Size(368, 398) };

        // ══ Tab: Appearance ═══════════════════════════════════════════════════
        var pAppear = MakeTab("Appearance");

        AddLabel(pAppear, "Bg opacity:", 12, 16);
        int initBg = (int)(current.BackgroundOpacity * 100);
        _bgOpacityValueLabel = MakeValueLabel(pAppear, initBg, 300, 18);
        _bgOpacityBar = MakeTrackBar(pAppear, initBg, 96, 10);
        _bgOpacityBar.ValueChanged += (_, _) => _bgOpacityValueLabel.Text = $"{_bgOpacityBar.Value}%";

        AddLabel(pAppear, "Text opacity:", 12, 56);
        int initText = (int)(current.TextOpacity * 100);
        _textOpacityValueLabel = MakeValueLabel(pAppear, initText, 300, 58);
        _textOpacityBar = MakeTrackBar(pAppear, initText, 96, 50);
        _textOpacityBar.ValueChanged += (_, _) => _textOpacityValueLabel.Text = $"{_textOpacityBar.Value}%";

        AddLabel(pAppear, "Font size:", 12, 96);
        _fontSpinner = new NumericUpDown
        {
            Minimum = 8, Maximum = 28, Value = (decimal)current.FontSize,
            DecimalPlaces = 0, Width = 58, Location = new Point(96, 93),
        };
        pAppear.Controls.Add(_fontSpinner);
        AddLabel(pAppear, "pt", 160, 96);

        AddLabel(pAppear, "Download:", 12, 140);
        _downColorBtn = MakeColorBtn(Color.FromArgb(current.DownloadColor));
        _downColorBtn.Location = new Point(86, 137);
        _downColorBtn.Click += (_, _) => PickColor(_downColorBtn);
        AddLabel(pAppear, "Upload:", 174, 140);
        _upColorBtn = MakeColorBtn(Color.FromArgb(current.UploadColor));
        _upColorBtn.Location = new Point(232, 137);
        _upColorBtn.Click += (_, _) => PickColor(_upColorBtn);

        _sparklineCheck = new CheckBox
        {
            Text = "Show speed graph (60-second sparkline)",
            Checked = current.ShowSparkline, Location = new Point(12, 178), AutoSize = true,
        };
        _showDownBarsCheck = new CheckBox
        {
            Text = "↓ Download bars", Checked = current.ShowDownBars,
            Enabled = current.ShowSparkline, Location = new Point(30, 204), AutoSize = true,
        };
        _showUpBarsCheck = new CheckBox
        {
            Text = "↑ Upload bars", Checked = current.ShowUpBars,
            Enabled = current.ShowSparkline, Location = new Point(190, 204), AutoSize = true,
        };
        _sparklineCheck.CheckedChanged += (_, _) =>
        {
            _showDownBarsCheck.Enabled = _sparklineCheck.Checked;
            _showUpBarsCheck.Enabled   = _sparklineCheck.Checked;
        };
        _showDownLineCheck = new CheckBox
        {
            Text = "Show ↓ line", Checked = current.ShowDownloadLine,
            Location = new Point(12, 232), AutoSize = true,
        };
        _showUpLineCheck = new CheckBox
        {
            Text = "Show ↑ line", Checked = current.ShowUploadLine,
            Location = new Point(190, 232), AutoSize = true,
        };
        pAppear.Controls.AddRange([_downColorBtn, _upColorBtn, _sparklineCheck,
            _showDownBarsCheck, _showUpBarsCheck, _showDownLineCheck, _showUpLineCheck]);

        // ══ Tab: Layout & Units ═══════════════════════════════════════════════
        var pLayout = MakeTab("Layout & Units");

        var orientGroup = MakeGroup("Orientation", 12, 12, 336, 52);
        _verticalRadio   = new RadioButton { Text = "Vertical",   Checked = !current.Horizontal, Location = new Point(12, 20), AutoSize = true };
        _horizontalRadio = new RadioButton { Text = "Horizontal", Checked = current.Horizontal,  Location = new Point(120, 20), AutoSize = true };
        orientGroup.Controls.AddRange([_verticalRadio, _horizontalRadio]);

        _compactCheck = new CheckBox
        {
            Text = "Compact mode (single tight row, both metrics)",
            Checked = current.CompactMode, Location = new Point(14, 74), AutoSize = true,
        };

        var unitsGroup = MakeGroup("Units", 12, 104, 336, 52);
        _binaryRadio  = new RadioButton { Text = "Binary  (KiB / MiB)", Checked = !current.DecimalUnits, Location = new Point(12, 20), AutoSize = true };
        _decimalRadio = new RadioButton { Text = "Decimal  (KB / MB)",  Checked = current.DecimalUnits,  Location = new Point(180, 20), AutoSize = true };
        unitsGroup.Controls.AddRange([_binaryRadio, _decimalRadio]);

        _bitsCheck = new CheckBox
        {
            Text = "Show speeds as bits (Mbps) — compare to ISP plan",
            Checked = current.ShowBits, Location = new Point(14, 166), AutoSize = true,
        };
        pLayout.Controls.AddRange([orientGroup, _compactCheck, unitsGroup, _bitsCheck]);

        // ══ Tab: Window ═══════════════════════════════════════════════════════
        var pWindow = MakeTab("Window");
        _alwaysOnTopCheck = new CheckBox { Text = "Always on top", Checked = current.AlwaysOnTop, Location = new Point(14, 16), AutoSize = true };
        _snapCheck        = new CheckBox { Text = "Snap to edges when dragging", Checked = current.SnapToEdges, Location = new Point(14, 42), AutoSize = true };
        _lockCheck        = new CheckBox { Text = "Lock position (disable dragging)", Checked = current.LockPosition, Location = new Point(14, 68), AutoSize = true };
        _clickThroughCheck = new CheckBox { Text = "Click-through  (interact via tray icon only)", Checked = current.ClickThrough, Location = new Point(14, 94), AutoSize = true };
        _startWithWindowsCheck = new CheckBox { Text = "Start with Windows", Checked = autoStart, Location = new Point(14, 120), AutoSize = true };
        _taskbarCheck = new CheckBox
        {
            Text = "Show speed indicator in the taskbar (next to tray)",
            Checked = current.ShowTaskbarIndicator, Location = new Point(14, 146), AutoSize = true,
        };
        AddLabel(pWindow, "Refresh rate:", 14, 178);
        _refreshCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(100, 176), Width = 72 };
        _refreshCombo.Items.AddRange(["1 s", "2 s", "5 s"]);
        _refreshCombo.SelectedIndex = current.RefreshIntervalMs switch { 2000 => 1, 5000 => 2, _ => 0 };
        pWindow.Controls.AddRange([_alwaysOnTopCheck, _snapCheck, _lockCheck,
            _clickThroughCheck, _startWithWindowsCheck, _taskbarCheck, _refreshCombo]);

        // ══ Tab: Network & Data ═══════════════════════════════════════════════
        var pNet = MakeTab("Network & Data");
        AddLabel(pNet, "Adapter:", 12, 18);
        _adapterCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(80, 16), Width = 260 };
        _adapterCombo.Items.Add("All adapters");
        foreach (var name in NetworkStats.GetAdapterNames())
            _adapterCombo.Items.Add(name);
        int idx = string.IsNullOrEmpty(current.AdapterName) ? 0 : _adapterCombo.Items.IndexOf(current.AdapterName);
        _adapterCombo.SelectedIndex = idx < 0 ? 0 : idx;

        _pingEnabledCheck = new CheckBox { Text = "Enable ping / latency monitoring", Checked = current.PingEnabled, Location = new Point(14, 54), AutoSize = true };
        AddLabel(pNet, "Ping host:", 12, 86);
        _pingHostText = new TextBox { Text = current.PingHost, Location = new Point(80, 83), Width = 160 };
        _pingEnabledCheck.CheckedChanged += (_, _) => _pingHostText.Enabled = _pingEnabledCheck.Checked;
        _pingHostText.Enabled = current.PingEnabled;

        AddLabel(pNet, "Monthly cap:", 12, 122);
        _capSpinner = new NumericUpDown
        {
            Minimum = 0, Maximum = 1_000_000, DecimalPlaces = 0, Increment = 10,
            Value = Math.Clamp(current.MonthlyCapBytes / GB, 0, 1_000_000),
            Width = 90, Location = new Point(100, 120),
        };
        AddLabel(pNet, "GB   (0 = no cap)", 196, 122);
        pNet.Controls.AddRange([_adapterCombo, _pingEnabledCheck, _pingHostText, _capSpinner]);

        tabs.TabPages.AddRange([pAppear, pLayout, pWindow, pNet]);
        Controls.Add(tabs);

        // ══ Buttons ═══════════════════════════════════════════════════════════
        var cancelBtn = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(220, 414), Size = new Size(74, 26) };
        var okBtn     = new Button { Text = "OK",     DialogResult = DialogResult.OK,     Location = new Point(300, 414), Size = new Size(74, 26) };
        Controls.AddRange([cancelBtn, okBtn]);
        AcceptButton = okBtn;
        CancelButton = cancelBtn;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        if (DialogResult == DialogResult.OK)
        {
            AutoStartResult = _startWithWindowsCheck.Checked;

            string adapter = _adapterCombo.SelectedIndex <= 0
                ? ""
                : _adapterCombo.SelectedItem?.ToString() ?? "";

            Result = new AppSettings
            {
                BackgroundOpacity = _bgOpacityBar.Value   / 100.0,
                TextOpacity       = _textOpacityBar.Value / 100.0,
                FontSize          = (float)_fontSpinner.Value,
                Horizontal        = _horizontalRadio.Checked,
                DecimalUnits      = _decimalRadio.Checked,
                AlwaysOnTop       = _alwaysOnTopCheck.Checked,
                ClickThrough      = _clickThroughCheck.Checked,
                ShowTaskbarIndicator = _taskbarCheck.Checked,
                ShowSparkline     = _sparklineCheck.Checked,
                ShowDownBars      = _showDownBarsCheck.Checked,
                ShowUpBars        = _showUpBarsCheck.Checked,
                ShowDownloadLine  = _showDownLineCheck.Checked,
                ShowUploadLine    = _showUpLineCheck.Checked,
                SnapToEdges       = _snapCheck.Checked,
                LockPosition      = _lockCheck.Checked,
                CompactMode       = _compactCheck.Checked,
                ShowBits          = _bitsCheck.Checked,
                DownloadColor     = _downColorBtn.BackColor.ToArgb(),
                UploadColor       = _upColorBtn.BackColor.ToArgb(),
                AdapterName       = adapter,
                RefreshIntervalMs = _refreshCombo.SelectedIndex switch { 1 => 2000, 2 => 5000, _ => 1000 },
                PingEnabled       = _pingEnabledCheck.Checked,
                PingHost          = string.IsNullOrWhiteSpace(_pingHostText.Text) ? "8.8.8.8" : _pingHostText.Text.Trim(),
                MonthlyCapBytes   = (long)_capSpinner.Value * GB,
                WindowX           = _current.WindowX,
                WindowY           = _current.WindowY,
                ShowPingOnWidget  = _current.ShowPingOnWidget,
            };
        }
        base.OnFormClosed(e);
    }

    private static TabPage MakeTab(string title) => new(title) { UseVisualStyleBackColor = true };

    private static void PickColor(Button btn)
    {
        using var dlg = new ColorDialog { Color = btn.BackColor, FullOpen = true };
        if (dlg.ShowDialog() == DialogResult.OK)
            btn.BackColor = dlg.Color;
    }

    private static Button MakeColorBtn(Color color) => new()
    {
        BackColor = color, FlatStyle = FlatStyle.Flat, Size = new Size(60, 24), Text = "",
        FlatAppearance = { BorderColor = Color.Gray },
    };

    private static TrackBar MakeTrackBar(Control parent, int value, int x, int y)
    {
        var bar = new TrackBar
        {
            Minimum = 10, Maximum = 100, Value = value,
            TickFrequency = 10, SmallChange = 5, Width = 200, Location = new Point(x, y),
        };
        parent.Controls.Add(bar);
        return bar;
    }

    private static Label MakeValueLabel(Control parent, int value, int x, int y)
    {
        var lbl = new Label
        {
            Text = $"{value}%", Location = new Point(x, y), Size = new Size(40, 20),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        parent.Controls.Add(lbl);
        return lbl;
    }

    private static GroupBox MakeGroup(string title, int x, int y, int w, int h) => new()
    {
        Text = title, Location = new Point(x, y), Size = new Size(w, h),
    };

    private static void AddLabel(Control parent, string text, int x, int y) =>
        parent.Controls.Add(new Label { Text = text, Location = new Point(x, y + 4), AutoSize = true });
}
