using System.Drawing;
using System.Windows.Forms;

namespace InternetSpeedApp;

/// <summary>
/// Day-by-day usage history table (newest first) over everything the
/// <see cref="UsageTracker"/> retains, with period totals and daily average.
/// Today's row refreshes live while the window is open.
/// </summary>
internal sealed class UsageHistoryForm : Form
{
    private readonly SpeedWindow _owner;
    private readonly ListView    _list;
    private readonly Label       _summary;
    private readonly System.Windows.Forms.Timer _refresh;

    private static readonly Color Bg      = Color.FromArgb(24, 24, 24);
    private static readonly Color RowBg   = Color.FromArgb(34, 34, 34);
    private static readonly Color TextDim = Color.FromArgb(160, 160, 160);

    internal UsageHistoryForm(SpeedWindow owner)
    {
        _owner = owner;

        Text            = "Speed Monitor — Usage History";
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition   = FormStartPosition.CenterScreen;
        ClientSize      = new Size(470, 520);
        MinimumSize     = new Size(420, 320);
        BackColor       = Bg;
        ForeColor       = Color.White;
        ShowInTaskbar   = true;
        Font            = new Font("Segoe UI", 9f);

        _list = new ListView
        {
            View          = View.Details,
            FullRowSelect = true,
            GridLines     = false,
            HeaderStyle   = ColumnHeaderStyle.Nonclickable,
            BackColor     = RowBg,
            ForeColor     = Color.White,
            BorderStyle   = BorderStyle.None,
            Dock          = DockStyle.Fill,
        };
        _list.Columns.Add("Date",       150, HorizontalAlignment.Left);
        _list.Columns.Add("Download",    95, HorizontalAlignment.Right);
        _list.Columns.Add("Upload",      95, HorizontalAlignment.Right);
        _list.Columns.Add("Total",       95, HorizontalAlignment.Right);

        var listHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 12, 12, 0), BackColor = Bg };
        listHost.Controls.Add(_list);

        _summary = new Label
        {
            Dock      = DockStyle.Bottom,
            Height    = 26,
            ForeColor = TextDim,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding   = new Padding(14, 0, 0, 0),
        };

        var buttons = new Panel { Dock = DockStyle.Bottom, Height = 44, BackColor = Bg };
        var exportBtn = MakeButton("Export CSV…", 12);
        exportBtn.Click += (_, _) => _owner.ExportUsageCsv(this);
        var closeBtn = MakeButton("Close", 0);
        closeBtn.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
        closeBtn.Location = new Point(ClientSize.Width - 96, 9);
        closeBtn.Click += (_, _) => Close();
        buttons.Controls.AddRange([exportBtn, closeBtn]);

        Controls.Add(listHost);
        Controls.Add(_summary);
        Controls.Add(buttons);

        Populate();

        // Keep today's row current while the window is open.
        _refresh = new System.Windows.Forms.Timer { Interval = 2000 };
        _refresh.Tick += (_, _) => UpdateTodayRow();
        _refresh.Start();
    }

    private void Populate()
    {
        bool dec = _owner.Settings.DecimalUnits;

        var days = _owner.Usage.OrderedDays()
            .OrderByDescending(d => d.date)
            .ToList();

        _list.BeginUpdate();
        _list.Items.Clear();

        long sumDown = 0, sumUp = 0;
        foreach (var (date, down, up) in days)
        {
            sumDown += down;
            sumUp   += up;

            var item = new ListViewItem(FormatDate(date));
            item.SubItems.Add(Format.Bytes(down, dec));
            item.SubItems.Add(Format.Bytes(up, dec));
            item.SubItems.Add(Format.Bytes(down + up, dec));
            item.UseItemStyleForSubItems = false;
            item.SubItems[1].ForeColor = Color.FromArgb(_owner.Settings.DownloadColor);
            item.SubItems[2].ForeColor = Color.FromArgb(_owner.Settings.UploadColor);
            item.SubItems[3].ForeColor = Color.White;
            _list.Items.Add(item);
        }
        _list.EndUpdate();

        if (days.Count == 0)
        {
            _summary.Text = "No usage recorded yet — data appears after the first day of monitoring.";
            return;
        }

        long total = sumDown + sumUp;
        long avg   = total / days.Count;
        _summary.Text =
            $"{days.Count} day{(days.Count == 1 ? "" : "s")}   •   " +
            $"Total ↓ {Format.Bytes(sumDown, dec)}  ↑ {Format.Bytes(sumUp, dec)}   •   " +
            $"Daily average {Format.Bytes(avg, dec)}";
    }

    private void UpdateTodayRow()
    {
        if (_list.Items.Count == 0) { Populate(); return; }

        // Rows are newest-first; a date rollover at midnight adds a new day.
        string todayLabel = FormatDate(DateTime.Now.Date);
        if (_list.Items[0].Text != todayLabel) { Populate(); return; }

        bool dec = _owner.Settings.DecimalUnits;
        var (down, up) = _owner.Usage.Today;
        _list.Items[0].SubItems[1].Text = Format.Bytes(down, dec);
        _list.Items[0].SubItems[2].Text = Format.Bytes(up, dec);
        _list.Items[0].SubItems[3].Text = Format.Bytes(down + up, dec);
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        bool dec = _owner.Settings.DecimalUnits;
        long sumDown = 0, sumUp = 0;
        int  count   = 0;
        foreach (var (_, down, up) in _owner.Usage.OrderedDays())
        {
            sumDown += down;
            sumUp   += up;
            count++;
        }
        if (count == 0) return;
        long total = sumDown + sumUp;
        _summary.Text =
            $"{count} day{(count == 1 ? "" : "s")}   •   " +
            $"Total ↓ {Format.Bytes(sumDown, dec)}  ↑ {Format.Bytes(sumUp, dec)}   •   " +
            $"Daily average {Format.Bytes(total / count, dec)}";
    }

    private static string FormatDate(DateTime d)
    {
        if (d == DateTime.Now.Date)              return "Today";
        if (d == DateTime.Now.Date.AddDays(-1))  return "Yesterday";
        return d.ToString("ddd, MMM d yyyy");
    }

    private static Button MakeButton(string text, int x) => new()
    {
        Text = text, Location = new Point(x, 9), Size = new Size(96, 26),
        FlatStyle = FlatStyle.Flat, ForeColor = Color.White,
        BackColor = Color.FromArgb(48, 48, 48),
        FlatAppearance = { BorderColor = Color.FromArgb(80, 80, 80) },
    };

    protected override void Dispose(bool disposing)
    {
        if (disposing) _refresh.Dispose();
        base.Dispose(disposing);
    }
}
