using System.Net.NetworkInformation;

namespace InternetSpeedApp;

internal static class NetworkStats
{
    internal static (long received, long sent) GetTotals()
    {
        long received = 0, sent = 0;
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            var stats = nic.GetIPv4Statistics();
            received += stats.BytesReceived;
            sent += stats.BytesSent;
        }
        return (received, sent);
    }
}
