using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace InternetSpeedApp;

internal static class NetworkStats
{
    /// <summary>First IPv4 address of an up, non-loopback adapter, or "—".</summary>
    internal static string GetLocalIPv4(string adapterName = "")
    {
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            if (!string.IsNullOrEmpty(adapterName) && nic.Name != adapterName) continue;
            foreach (var ua in nic.GetIPProperties().UnicastAddresses)
                if (ua.Address.AddressFamily == AddressFamily.InterNetwork)
                    return ua.Address.ToString();
        }
        return "—";
    }

    /// <summary>Connected Wi-Fi SSID via netsh, or "" if not on Wi-Fi.</summary>
    internal static string GetWifiSsid()
    {
        try
        {
            var psi = new ProcessStartInfo("netsh", "wlan show interfaces")
            {
                RedirectStandardOutput = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };
            using var proc = Process.Start(psi);
            if (proc is null) return "";
            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(2000);

            foreach (var raw in output.Split('\n'))
            {
                var line = raw.Trim();
                // Match "SSID" but not "BSSID"; the ':' separates the value.
                if (line.StartsWith("SSID", StringComparison.OrdinalIgnoreCase)
                    && !line.StartsWith("BSSID", StringComparison.OrdinalIgnoreCase))
                {
                    int colon = line.IndexOf(':');
                    if (colon >= 0) return line[(colon + 1)..].Trim();
                }
            }
        }
        catch { }
        return "";
    }

    internal static (long received, long sent) GetTotals(string adapterName = "")
    {
        long received = 0, sent = 0;
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            if (!string.IsNullOrEmpty(adapterName) && nic.Name != adapterName) continue;
            var stats = nic.GetIPv4Statistics();
            received += stats.BytesReceived;
            sent     += stats.BytesSent;
        }
        return (received, sent);
    }

    internal static IReadOnlyList<string> GetAdapterNames() =>
        [.. NetworkInterface.GetAllNetworkInterfaces()
               .Where(n => n.OperationalStatus == OperationalStatus.Up
                        && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
               .Select(n => n.Name)];
}
