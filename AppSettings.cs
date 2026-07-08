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
    public double BackgroundOpacity { get; set; } = 0.85;
    public double TextOpacity       { get; set; } = 1.0;
    public int    DownloadColor     { get; set; } = Color.FromArgb(60, 220, 60).ToArgb();
    public int    UploadColor       { get; set; } = Color.FromArgb(255, 160, 30).ToArgb();

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
