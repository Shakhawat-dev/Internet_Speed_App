namespace InternetSpeedApp.Core;

/// <summary>
/// The measurement engine: converts cumulative interface byte counters into
/// per-interval speeds, accumulates session totals, and keeps a ring buffer of
/// recent speeds for the sparkline. UI-free; the widget drives it from a timer.
/// </summary>
internal sealed class SpeedSampler
{
    internal const int HistoryLength = 60;

    private long     _lastReceived, _lastSent;
    private DateTime _lastSampleTime;

    private readonly long[] _downHistory = new long[HistoryLength];
    private readonly long[] _upHistory   = new long[HistoryLength];

    /// <summary>Most recent speeds in bytes/second.</summary>
    internal long CurrentDown { get; private set; }
    internal long CurrentUp   { get; private set; }

    /// <summary>Cumulative bytes transferred since app start (or last reset).</summary>
    internal long SessionDown { get; private set; }
    internal long SessionUp   { get; private set; }

    /// <summary>Speed history ring buffers; oldest entry is at <see cref="HistoryIndex"/>.</summary>
    internal IReadOnlyList<long> DownHistory => _downHistory;
    internal IReadOnlyList<long> UpHistory   => _upHistory;
    internal int HistoryIndex { get; private set; }

    internal SpeedSampler()
    {
        (_lastReceived, _lastSent) = NetworkStats.GetTotals();
        _lastSampleTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Takes one sample and updates speeds, session totals, and history.
    /// Returns the raw byte deltas since the previous sample (for usage tracking).
    /// </summary>
    internal (long downBytes, long upBytes) Sample(string adapterName)
    {
        var (received, sent) = NetworkStats.GetTotals(adapterName);
        var now = DateTime.UtcNow;
        double elapsed = (now - _lastSampleTime).TotalSeconds;

        // Counters can go backwards when an adapter resets — clamp to zero.
        long downBytes = Math.Max(0, received - _lastReceived);
        long upBytes   = Math.Max(0, sent - _lastSent);

        CurrentDown = elapsed > 0 ? (long)(downBytes / elapsed) : 0;
        CurrentUp   = elapsed > 0 ? (long)(upBytes / elapsed)   : 0;

        _lastReceived   = received;
        _lastSent       = sent;
        _lastSampleTime = now;

        SessionDown += downBytes;
        SessionUp   += upBytes;

        _downHistory[HistoryIndex] = CurrentDown;
        _upHistory[HistoryIndex]   = CurrentUp;
        HistoryIndex = (HistoryIndex + 1) % HistoryLength;

        return (downBytes, upBytes);
    }

    internal void ResetSession()
    {
        SessionDown = 0;
        SessionUp   = 0;
    }
}
