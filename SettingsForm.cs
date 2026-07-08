using System.Drawing;
using System.Windows.Forms;

namespace InternetSpeedApp;

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
    // Window
    private readonly CheckBox      _startWithWindowsCheck;
    private readonly CheckBox      _snapCheck;
    private readonly ComboBox      _refreshCombo;

    internal bool AutoStartResult { get; private set; }
    // Layout
    private readonly RadioButton   _verticalRadio;
    private readonly RadioButton   _horizontalRadio;
    // Units
    private readonly RadioButton   _binaryRadio;
    private readonly RadioButton   _decimalRadio;
    // Window
    private readonly CheckBox      _alwaysOnTopCheck;
    private readonly CheckBox      _clickThroughCheck;
    // Network
    private readonly ComboBox      _adapterCombo;

    internal AppSettings Result { get; private set; }

    private readonly AppSettings _current;

    internal SettingsForm(AppSettings current, bool autoStart)
    {
        Result   = current;
        _current = current;

        Text            = "Speed Monitor — Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition   = FormStartPosition.CenterScreen;
        ClientSize      = new Size(360, 658);
        MaximizeBox     = false;
        MinimizeBox     = false;
        ShowInTaskbar   = false;

        // ── Appearance (y=10, h=220) ─────────────────────────────────────────
        var appearBox = MakeGroup("Appearance", 10, 10, 340, 266);

        AddLabel(appearBox, "Bg opacity:", 10, 22);
        int initBg = (int)(current.BackgroundOpacity * 100);
        _bgOpacityValueLabel = MakeValueLabel(appearBox, initBg, 292, 24);
        _bgOpacityBar = MakeTrackBar(appearBox, initBg, 90, 16);
        _bgOpacityBar.ValueChanged += (_, _) => _bgOpacityValueLabel.Text = $"{_bgOpacityBar.Value}%";

        AddLabel(appearBox, "Text opacity:", 10, 62);
        int initText = (int)(current.TextOpacity * 100);
        _textOpacityValueLabel = MakeValueLabel(appearBox, initText, 292, 64);
        _textOpacityBar = MakeTrackBar(appearBox, initText, 90, 56);
        _textOpacityBar.ValueChanged += (_, _) => _textOpacityValueLabel.Text = $"{_textOpacityBar.Value}%";

        AddLabel(appearBox, "Font size:", 10, 102);
        _fontSpinner = new NumericUpDown
        {
            Minimum = 8, Maximum = 28,
            Value = (decimal)current.FontSize,
            DecimalPlaces = 0, Width = 58,
            Location = new Point(90, 99),
        };
        appearBox.Controls.Add(_fontSpinner);
        AddLabel(appearBox, "pt", 154, 102);

        AddLabel(appearBox, "Download:", 10, 148);
        _downColorBtn = MakeColorBtn(Color.FromArgb(current.DownloadColor));
        _downColorBtn.Location = new Point(84, 145);
        _downColorBtn.Click += (_, _) => PickColor(_downColorBtn);
        appearBox.Controls.Add(_downColorBtn);

        AddLabel(appearBox, "Upload:", 162, 148);
        _upColorBtn = MakeColorBtn(Color.FromArgb(current.UploadColor));
        _upColorBtn.Location = new Point(220, 145);
        _upColorBtn.Click += (_, _) => PickColor(_upColorBtn);
        appearBox.Controls.Add(_upColorBtn);

        _sparklineCheck = new CheckBox
        {
            Text = "Show speed graph (60-second sparkline)",
            Checked = current.ShowSparkline,
            Location = new Point(10, 186), AutoSize = true,
        };

        _showDownBarsCheck = new CheckBox
        {
            Text = "↓ Download bars",
            Checked = current.ShowDownBars,
            Enabled = current.ShowSparkline,
            Location = new Point(28, 212), AutoSize = true,
        };
        _showUpBarsCheck = new CheckBox
        {
            Text = "↑ Upload bars",
            Checked = current.ShowUpBars,
            Enabled = current.ShowSparkline,
            Location = new Point(175, 212), AutoSize = true,
        };
        _sparklineCheck.CheckedChanged += (_, _) =>
        {
            _showDownBarsCheck.Enabled = _sparklineCheck.Checked;
            _showUpBarsCheck.Enabled   = _sparklineCheck.Checked;
        };

        _showDownLineCheck = new CheckBox
        {
            Text = "Show ↓ line",
            Checked = current.ShowDownloadLine,
            Location = new Point(10, 238), AutoSize = true,
        };
        _showUpLineCheck = new CheckBox
        {
            Text = "Show ↑ line",
            Checked = current.ShowUploadLine,
            Location = new Point(175, 238), AutoSize = true,
        };

        appearBox.Controls.AddRange([_sparklineCheck, _showDownBarsCheck, _showUpBarsCheck,
                                     _showDownLineCheck, _showUpLineCheck]);

        Controls.Add(appearBox);

        // ── Layout (y=240, h=58) ─────────────────────────────────────────────
        var layoutBox = MakeGroup("Layout", 10, 286, 340, 58);
        _verticalRadio = new RadioButton
        {
            Text = "Vertical", Checked = !current.Horizontal,
            Location = new Point(10, 22), AutoSize = true,
        };
        _horizontalRadio = new RadioButton
        {
            Text = "Horizontal", Checked = current.Horizontal,
            Location = new Point(100, 22), AutoSize = true,
        };
        layoutBox.Controls.AddRange([_verticalRadio, _horizontalRadio]);
        Controls.Add(layoutBox);

        // ── Units (y=308, h=58) ──────────────────────────────────────────────
        var unitsBox = MakeGroup("Units", 10, 354, 340, 58);
        _binaryRadio = new RadioButton
        {
            Text = "Binary  (KiB / MiB)", Checked = !current.DecimalUnits,
            Location = new Point(10, 22), AutoSize = true,
        };
        _decimalRadio = new RadioButton
        {
            Text = "Decimal  (KB / MB)", Checked = current.DecimalUnits,
            Location = new Point(170, 22), AutoSize = true,
        };
        unitsBox.Controls.AddRange([_binaryRadio, _decimalRadio]);
        Controls.Add(unitsBox);

        // ── Window (y=376, h=74) ─────────────────────────────────────────────
        var windowBox = MakeGroup("Window", 10, 422, 340, 130);
        _alwaysOnTopCheck = new CheckBox
        {
            Text = "Always on top", Checked = current.AlwaysOnTop,
            Location = new Point(10, 20), AutoSize = true,
        };
        _snapCheck = new CheckBox
        {
            Text = "Snap to edges", Checked = current.SnapToEdges,
            Location = new Point(175, 20), AutoSize = true,
        };
        _clickThroughCheck = new CheckBox
        {
            Text = "Click-through  (interact via tray icon only)",
            Checked = current.ClickThrough,
            Location = new Point(10, 46), AutoSize = true,
        };
        _startWithWindowsCheck = new CheckBox
        {
            Text = "Start with Windows",
            Checked = autoStart,
            Location = new Point(10, 72), AutoSize = true,
        };
        AddLabel(windowBox, "Refresh:", 10, 98);
        _refreshCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(78, 96), Width = 72,
        };
        _refreshCombo.Items.AddRange(["1 s", "2 s", "5 s"]);
        _refreshCombo.SelectedIndex = current.RefreshIntervalMs switch { 2000 => 1, 5000 => 2, _ => 0 };
        windowBox.Controls.AddRange([_alwaysOnTopCheck, _snapCheck, _clickThroughCheck,
                                     _startWithWindowsCheck, _refreshCombo]);
        Controls.Add(windowBox);

        // ── Network (y=460, h=58) ────────────────────────────────────────────
        var networkBox = MakeGroup("Network", 10, 562, 340, 58);
        AddLabel(networkBox, "Adapter:", 10, 20);

        _adapterCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(78, 18), Width = 248,
        };
        _adapterCombo.Items.Add("All adapters");
        foreach (var name in NetworkStats.GetAdapterNames())
            _adapterCombo.Items.Add(name);

        // Select current adapter (or "All adapters" if not found / empty)
        int idx = string.IsNullOrEmpty(current.AdapterName)
            ? 0
            : _adapterCombo.Items.IndexOf(current.AdapterName);
        _adapterCombo.SelectedIndex = idx < 0 ? 0 : idx;

        networkBox.Controls.Add(_adapterCombo);
        Controls.Add(networkBox);

        // ── Buttons ──────────────────────────────────────────────────────────
        var cancelBtn = new Button
        {
            Text = "Cancel", DialogResult = DialogResult.Cancel,
            Location = new Point(196, 628), Size = new Size(74, 26),
        };
        var okBtn = new Button
        {
            Text = "OK", DialogResult = DialogResult.OK,
            Location = new Point(276, 628), Size = new Size(74, 26),
        };
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
                ShowSparkline     = _sparklineCheck.Checked,
                ShowDownBars      = _showDownBarsCheck.Checked,
                ShowUpBars        = _showUpBarsCheck.Checked,
                ShowDownloadLine  = _showDownLineCheck.Checked,
                ShowUploadLine    = _showUpLineCheck.Checked,
                SnapToEdges       = _snapCheck.Checked,
                DownloadColor     = _downColorBtn.BackColor.ToArgb(),
                UploadColor       = _upColorBtn.BackColor.ToArgb(),
                AdapterName       = adapter,
                RefreshIntervalMs = _refreshCombo.SelectedIndex switch { 1 => 2000, 2 => 5000, _ => 1000 },
                WindowX           = _current.WindowX,
                WindowY           = _current.WindowY,
            };
        }
        base.OnFormClosed(e);
    }

    private static void PickColor(Button btn)
    {
        using var dlg = new ColorDialog { Color = btn.BackColor, FullOpen = true };
        if (dlg.ShowDialog() == DialogResult.OK)
            btn.BackColor = dlg.Color;
    }

    private static Button MakeColorBtn(Color color) => new()
    {
        BackColor = color,
        FlatStyle = FlatStyle.Flat,
        Size      = new Size(60, 24),
        Text      = "",
        FlatAppearance = { BorderColor = Color.Gray },
    };

    private static TrackBar MakeTrackBar(Control parent, int value, int x, int y)
    {
        var bar = new TrackBar
        {
            Minimum = 10, Maximum = 100,
            Value = value,
            TickFrequency = 10, SmallChange = 5,
            Width = 190, Location = new Point(x, y),
        };
        parent.Controls.Add(bar);
        return bar;
    }

    private static Label MakeValueLabel(Control parent, int value, int x, int y)
    {
        var lbl = new Label
        {
            Text = $"{value}%",
            Location = new Point(x, y), Size = new Size(38, 20),
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
        parent.Controls.Add(new Label
        {
            Text = text, Location = new Point(x, y + 4), AutoSize = true,
        });
}
