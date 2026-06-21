using System.Drawing;
using System.Windows.Forms;

namespace InternetSpeedApp;

internal sealed class SettingsForm : Form
{
    private readonly TrackBar       _opacityBar;
    private readonly Label          _opacityValueLabel;
    private readonly NumericUpDown  _fontSpinner;
    private readonly RadioButton    _verticalRadio;
    private readonly RadioButton    _horizontalRadio;
    private readonly RadioButton    _binaryRadio;
    private readonly RadioButton    _decimalRadio;

    internal AppSettings Result { get; private set; }

    internal SettingsForm(AppSettings current)
    {
        Result = current;

        Text            = "Speed Monitor — Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition   = FormStartPosition.CenterScreen;
        ClientSize      = new Size(360, 286);
        MaximizeBox     = false;
        MinimizeBox     = false;
        ShowInTaskbar   = false;

        // ── Appearance ──────────────────────────────────────────────────────
        var appearBox = MakeGroup("Appearance", 10, 10, 340, 102);

        AddLabel(appearBox, "Opacity:", 10, 22);
        int initialOpacity = (int)(current.Opacity * 100);
        _opacityValueLabel = new Label
        {
            Text = $"{initialOpacity}%",
            Location = new Point(292, 24), Size = new Size(38, 20),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        appearBox.Controls.Add(_opacityValueLabel);

        _opacityBar = new TrackBar
        {
            Minimum = 20, Maximum = 100,
            Value = initialOpacity,
            TickFrequency = 10, SmallChange = 5,
            Width = 202, Location = new Point(84, 16),
        };
        _opacityBar.ValueChanged += (_, _) => _opacityValueLabel.Text = $"{_opacityBar.Value}%";
        appearBox.Controls.Add(_opacityBar);

        AddLabel(appearBox, "Font size:", 10, 62);
        _fontSpinner = new NumericUpDown
        {
            Minimum = 8, Maximum = 28,
            Value = (decimal)current.FontSize,
            DecimalPlaces = 0, Width = 58,
            Location = new Point(84, 59),
        };
        appearBox.Controls.Add(_fontSpinner);
        AddLabel(appearBox, "pt", 148, 62);

        Controls.Add(appearBox);

        // ── Layout ──────────────────────────────────────────────────────────
        var layoutBox = MakeGroup("Layout", 10, 122, 340, 58);

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
        var unitsBox = MakeGroup("Units", 10, 190, 340, 58);

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

        // ── Buttons ──────────────────────────────────────────────────────────
        var cancelBtn = new Button
        {
            Text = "Cancel", DialogResult = DialogResult.Cancel,
            Location = new Point(196, 258), Size = new Size(74, 26),
        };
        var okBtn = new Button
        {
            Text = "OK", DialogResult = DialogResult.OK,
            Location = new Point(276, 258), Size = new Size(74, 26),
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
                Opacity      = _opacityBar.Value / 100.0,
                FontSize     = (float)_fontSpinner.Value,
                Horizontal   = _horizontalRadio.Checked,
                DecimalUnits = _decimalRadio.Checked,
            };
        }
        base.OnFormClosed(e);
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
