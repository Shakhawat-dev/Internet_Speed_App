using System.Text.Json;
using System.Text.Json.Serialization;

namespace InternetSpeedApp.Core;

/// <summary>
/// Tracks cumulative download/upload bytes per calendar day, persisted to
/// <c>%APPDATA%\InternetSpeedApp\usage.json</c>. Provides today's and the
/// current month's totals, rolls over automatically at midnight, and prunes
/// entries older than <see cref="RetentionDays"/> to bound the file size.
/// </summary>
internal sealed class UsageTracker
{
    private const int RetentionDays = 120;

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "InternetSpeedApp", "usage.json");

    public sealed class DayEntry
    {
        public long Down { get; set; }
        public long Up   { get; set; }
    }

    // Keyed by "yyyy-MM-dd".
    [JsonInclude]
    public Dictionary<string, DayEntry> Days { get; private set; } = new();

    [JsonIgnore]
    private static string TodayKey => DateTime.Now.ToString("yyyy-MM-dd");

    internal static UsageTracker Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var t = JsonSerializer.Deserialize<UsageTracker>(File.ReadAllText(FilePath));
                if (t is not null) { t.Prune(); return t; }
            }
        }
        catch { }
        return new UsageTracker();
    }

    internal void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath,
                JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    /// <summary>Adds transferred bytes to today's running total.</summary>
    internal void Add(long downBytes, long upBytes)
    {
        if (downBytes <= 0 && upBytes <= 0) return;
        if (!Days.TryGetValue(TodayKey, out var entry))
        {
            entry = new DayEntry();
            Days[TodayKey] = entry;
        }
        entry.Down += downBytes;
        entry.Up   += upBytes;
    }

    internal (long down, long up) Today =>
        Days.TryGetValue(TodayKey, out var e) ? (e.Down, e.Up) : (0, 0);

    internal (long down, long up) Month
    {
        get
        {
            string prefix = DateTime.Now.ToString("yyyy-MM");
            long down = 0, up = 0;
            foreach (var (key, e) in Days)
            {
                if (!key.StartsWith(prefix, StringComparison.Ordinal)) continue;
                down += e.Down;
                up   += e.Up;
            }
            return (down, up);
        }
    }

    /// <summary>Ordered (oldest → newest) day entries, for charting.</summary>
    internal IEnumerable<(DateTime date, long down, long up)> OrderedDays()
    {
        foreach (var (key, e) in Days)
            if (DateTime.TryParse(key, out var d))
                yield return (d, e.Down, e.Up);
    }

    private void Prune()
    {
        var cutoff = DateTime.Now.Date.AddDays(-RetentionDays);
        var stale = Days.Keys
            .Where(k => DateTime.TryParse(k, out var d) && d < cutoff)
            .ToList();
        foreach (var k in stale) Days.Remove(k);
    }
}
