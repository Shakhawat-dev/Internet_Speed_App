using System.Drawing;
using System.Text.Json;

namespace InternetSpeedApp;

internal sealed class AppSettings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "InternetSpeedApp", "settings.json");

    public float  FontSize          { get; set; } = 13f;
    public bool   Horizontal        { get; set; } = false;
    public bool   DecimalUnits      { get; set; } = false;
    public bool   AlwaysOnTop       { get; set; } = true;
    public bool   ClickThrough      { get; set; } = false;
    public bool   ShowSparkline     { get; set; } = true;
    public bool   ShowDownBars      { get; set; } = true;
    public bool   ShowUpBars        { get; set; } = true;
    public double BackgroundOpacity { get; set; } = 0.85;
    public double TextOpacity       { get; set; } = 1.0;
    public int    DownloadColor     { get; set; } = Color.FromArgb(60, 220, 60).ToArgb();
    public int    UploadColor       { get; set; } = Color.FromArgb(255, 160, 30).ToArgb();
    public string AdapterName       { get; set; } = "";  // empty = all adapters
    public int    WindowX           { get; set; } = int.MinValue;  // int.MinValue = use default position
    public int    WindowY           { get; set; } = int.MinValue;
    public int    RefreshIntervalMs { get; set; } = 1000;
    public bool   ShowDownloadLine  { get; set; } = true;
    public bool   ShowUploadLine    { get; set; } = true;
    public bool   SnapToEdges       { get; set; } = true;
    public bool   PingEnabled       { get; set; } = true;
    public string PingHost          { get; set; } = "8.8.8.8";
    public bool   ShowPingOnWidget  { get; set; } = false;
    public long   MonthlyCapBytes   { get; set; } = 0;  // 0 = no cap
    public bool   ShowBits          { get; set; } = false;  // Mbps instead of MiB/s
    public bool   CompactMode       { get; set; } = false;  // single combined line
    public bool   LockPosition      { get; set; } = false;

    internal static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new();
        }
        catch { }
        return new();
    }

    internal void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
