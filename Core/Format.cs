namespace InternetSpeedApp.Core;

/// <summary>Shared byte / speed formatting used by the widget and dashboard.</summary>
internal static class Format
{
    /// <summary>Formats a per-second byte rate, e.g. "5.2 MiB/s".</summary>
    internal static string Speed(long bytesPerSec, bool decimalUnits)
    {
        int div      = decimalUnits ? 1000 : 1024;
        string kUnit = decimalUnits ? "KB/s" : "KiB/s";
        string mUnit = decimalUnits ? "MB/s" : "MiB/s";
        if (bytesPerSec >= (long)div * div) return $"{bytesPerSec / ((double)div * div):F1} {mUnit}";
        if (bytesPerSec >= div)             return $"{bytesPerSec / (double)div:F0} {kUnit}";
        return $"{bytesPerSec} B/s";
    }

    /// <summary>Formats a per-second byte rate as megabits, e.g. "42.3 Mbps".</summary>
    internal static string SpeedBits(long bytesPerSec)
    {
        double mbps = bytesPerSec * 8.0 / 1_000_000.0;
        double kbps = bytesPerSec * 8.0 / 1_000.0;
        if (mbps >= 1) return $"{mbps:F1} Mbps";
        if (kbps >= 1) return $"{kbps:F0} Kbps";
        return $"{bytesPerSec * 8} bps";
    }

    /// <summary>Formats a cumulative byte total, e.g. "4.2 GiB".</summary>
    internal static string Bytes(long bytes, bool decimalUnits)
    {
        int div = decimalUnits ? 1000 : 1024;
        string[] units = decimalUnits
            ? ["B", "KB", "MB", "GB", "TB"]
            : ["B", "KiB", "MiB", "GiB", "TiB"];
        double val = bytes;
        int u = 0;
        while (val >= div && u < units.Length - 1) { val /= div; u++; }
        return u == 0 ? $"{bytes} {units[0]}" : $"{val:F1} {units[u]}";
    }
}
