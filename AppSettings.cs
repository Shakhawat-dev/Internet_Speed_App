using System.Text.Json;

namespace InternetSpeedApp;

internal sealed class AppSettings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "InternetSpeedApp", "settings.json");

    public double Opacity  { get; set; } = 0.93;
    public float  FontSize { get; set; } = 13f;
    public bool   Horizontal    { get; set; } = false;
    public bool   DecimalUnits  { get; set; } = false; // false = KiB/MiB (1024), true = KB/MB (1000)
    public bool   AlwaysOnTop   { get; set; } = true;

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
