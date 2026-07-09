using System.Net.NetworkInformation;

namespace InternetSpeedApp.Core;

/// <summary>
/// Periodically pings a host and exposes the most recent round-trip time and
/// reachability. Pings are fire-and-forget; overlapping requests are skipped.
/// </summary>
internal sealed class PingMonitor
{
    private readonly Ping _ping = new();
    private bool _inFlight;

    /// <summary>Latest round-trip time in ms, or -1 if the last ping failed.</summary>
    internal int LatestMs { get; private set; } = -1;

    /// <summary>True when the most recent ping succeeded.</summary>
    internal bool IsUp => LatestMs >= 0;

    /// <summary>The host currently being pinged.</summary>
    internal string Host { get; set; } = "8.8.8.8";

    /// <summary>Sends one ping if none is already in flight. Safe to call often.</summary>
    internal async Task PingOnceAsync()
    {
        if (_inFlight) return;
        _inFlight = true;
        try
        {
            var reply = await _ping.SendPingAsync(Host, 2000);
            LatestMs = reply.Status == IPStatus.Success ? (int)reply.RoundtripTime : -1;
        }
        catch
        {
            LatestMs = -1;
        }
        finally
        {
            _inFlight = false;
        }
    }
}
