using System.Diagnostics;
using System.Net.NetworkInformation;

namespace VAK.Core.Sys;

public class WindowsNetworkService : INetworkService
{
    private long _lastDown, _lastUp;
    private DateTime _lastAt;
    private string _ssid = "";
    private DateTime _ssidAt = DateTime.MinValue;

    public NetworkInfo? Read()
    {
        try
        {
            var nic = NetworkInterface.GetAllNetworkInterfaces()
                .Where(IsRealConnection)
                .OrderByDescending(n => n.Speed)
                .FirstOrDefault();
            if (nic is null) return null;

            var stats = nic.GetIPv4Statistics();
            var now = DateTime.UtcNow;
            double down = 0, up = 0;
            if (_lastAt != default)
            {
                var dt = now - _lastAt;
                down = RateCalculator.BytesPerSecond(_lastDown, stats.BytesReceived, dt);
                up = RateCalculator.BytesPerSecond(_lastUp, stats.BytesSent, dt);
            }
            _lastDown = stats.BytesReceived; _lastUp = stats.BytesSent; _lastAt = now;

            var wireless = nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211;
            var name = wireless ? Ssid() : nic.Name;
            int? mbps = nic.Speed > 0 ? (int)(nic.Speed / 1_000_000) : null;
            return new NetworkInfo(string.IsNullOrEmpty(name) ? nic.Name : name, mbps, down, up, wireless);
        }
        catch { return null; }
    }

    private static readonly string[] VirtualHints =
        { "loopback", "npcap", "virtual", "vmware", "vbox", "hyper-v", "vethernet", "bluetooth", "tap-", "wintun", "wireguard" };

    /// <summary>A connected Wi-Fi or Ethernet adapter that has a default gateway and is not a virtual or loopback adapter.</summary>
    private static bool IsRealConnection(NetworkInterface n)
    {
        if (n.OperationalStatus != OperationalStatus.Up) return false;
        if (n.NetworkInterfaceType is not (NetworkInterfaceType.Wireless80211 or NetworkInterfaceType.Ethernet)) return false;
        if (VirtualHints.Any(h => n.Name.Contains(h, StringComparison.OrdinalIgnoreCase) ||
                                  n.Description.Contains(h, StringComparison.OrdinalIgnoreCase))) return false;
        return n.GetIPProperties().GatewayAddresses.Any(g =>
            g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !g.Address.Equals(System.Net.IPAddress.Any));
    }

    // netsh is slow, so the SSID is refreshed at most every 15 seconds.
    private string Ssid()
    {
        if (DateTime.UtcNow - _ssidAt < TimeSpan.FromSeconds(15)) return _ssid;
        _ssidAt = DateTime.UtcNow;
        try
        {
            using var p = Process.Start(new ProcessStartInfo("netsh", "wlan show interfaces")
            { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true })!;
            var text = p.StandardOutput.ReadToEnd();
            p.WaitForExit(2000);
            foreach (var line in text.Split('\n'))
            {
                var t = line.Trim();
                if (t.StartsWith("SSID") && !t.StartsWith("SSID name") || t.StartsWith("SSID  "))
                {
                    var i = t.IndexOf(':');
                    if (i > 0 && !t.StartsWith("BSSID")) { _ssid = t[(i + 1)..].Trim(); break; }
                }
            }
        }
        catch { _ssid = ""; }
        return _ssid;
    }
}
