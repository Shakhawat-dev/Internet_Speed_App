using System.Drawing;
using System.Windows.Forms;

namespace InternetSpeedApp;

internal sealed class SettingsForm : Form
{
    private readonly TrackBar      _bgOpacityBar;
    private readonly Label         _bgOpacityValueLabel;
    private readonly TrackBar      _textOpacityBar;
    private readonly Label         _textOpacityValueLabel;
    private readonly NumericUpDown _fontSpinner;
    private readonly RadioButton   _verticalRadio;
    private readonly RadioButton   _horizontalRadio;
    private readonly RadioButton   _binaryRadio;
    private readonly RadioButton   _decimalRadio;
    private readonly CheckBox      _alwaysOnTopCheck;
    private readonly Button        _downColorBtn;
    private readonly Button        _upColorBtn;

    internal AppSettings Result { get; private set; }

    internal SettingsForm(AppSettings current)
    {
        Result = current;

        Text            = "Speed Monitor — Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition   = FormStartPosition.CenterScreen;
        ClientSize      = new Size(360, 438);
        MaximizeBox     = false;
        MinimizeBox     = false;
        ShowInTaskbar   = false;

        // ── Appearance ──────────────────────────────────────────────────────
        var appearBox = MakeGroup("Appearance", 10, 10, 340, 196);

        // Background opacity
        AddLabel(appearBox, "Bg opacity:", 10, 22);
        int initBg = (int)(current.BackgroundOpacity * 100);
        _bgOpacityValueLabel = MakeValueLabel(appearBox, initBg, 292, 24);
        _bgOpacityBar = MakeTrackBar(appearBox, initBg, 90, 16);
        _bgOpacityBar.ValueChanged += (_, _) => _bgOpacityValueLabel.Text = $"{_bgOpacityBar.Value}%";

        // Text opacity
        AddLabel(appearBox, "Text opacity:", 10, 62);
        int initText = (int)(current.TextOpacity * 100);
        _textOpacityValueLabel = MakeValueLabel(appearBox, initText, 292, 64);
        _textOpacityBar = MakeTrackBar(appearBox, initText, 90, 56);
        _textOpacityBar.ValueChanged += (_, _) => _textOpacityValueLabel.Text = $"{_textOpacityBar.Value}%";

        // Font size
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

        // Colors
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

        Controls.Add(appearBox);

        // ── Layout ──────────────────────────────────────────────────────────
        var layoutBox = MakeGroup("Layout", 10, 216, 340, 58);
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

        // ── Units ────────────────────────────────────────────────────────────
        var unitsBox = MakeGroup("Units", 10, 284, 340, 58);
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

        // ── Window ───────────────────────────────────────────────────────────
        var windowBox = MakeGroup("Window", 10, 352, 340, 50);
        _alwaysOnTopCheck = new CheckBox
        {
            Text = "Always on top", Checked = current.AlwaysOnTop,
            Location = new Point(10, 20), AutoSize = true,
        };
        windowBox.Controls.Add(_alwaysOnTopCheck);
        Controls.Add(windowBox);

        // ── Buttons ──────────────────────────────────────────────────────────
        var cancelBtn = new Button
        {
            Text = "Cancel", DialogResult = DialogResult.Cancel,
            Location = new Point(196, 410), Size = new Size(74, 26),
        };
        var okBtn = new Button
        {
            Text = "OK", DialogResult = DialogResult.OK,
            Location = new Point(276, 410), Size = new Size(74, 26),
        };
        Controls.AddRange([cancelBtn, okBtn]);
        AcceptButton = okBtn;
        CancelButton = cancelBtn;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        if (DialogResult == DialogResult.OK)
        {
            Result = new AppSettings
            {
                BackgroundOpacity = _bgOpacityBar.Value   / 100.0,
                TextOpacity       = _textOpacityBar.Value / 100.0,
                FontSize          = (float)_fontSpinner.Value,
                Horizontal        = _horizontalRadio.Checked,
                DecimalUnits      = _decimalRadio.Checked,
                AlwaysOnTop       = _alwaysOnTopCheck.Checked,
                DownloadColor     = _downColorBtn.BackColor.ToArgb(),
                UploadColor       = _upColorBtn.BackColor.ToArgb(),
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
