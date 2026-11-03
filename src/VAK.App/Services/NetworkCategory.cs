using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using VAK.Core.Drop;

namespace VAK.App.Services;

public enum NetworkState { Private, Public, NoPrivateAddress }

/// <summary>Decides whether Drop may run: this PC needs a private LAN address, and Windows must not label the
/// connection "Public" (a public Wi-Fi such as a cafe or airport).</summary>
public static class NetworkCategory
{
    public static (NetworkState State, IPAddress? Address) Check()
    {
        var address = PrivateNetwork.Pick(LocalAddresses());
        if (address is null) return (NetworkState.NoPrivateAddress, null);
        return (IsAnyConnectionPublic() ? NetworkState.Public : NetworkState.Private, address);
    }

    private static IEnumerable<IPAddress> LocalAddresses()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                            n.NetworkInterfaceType is NetworkInterfaceType.Wireless80211 or NetworkInterfaceType.Ethernet)
                .Where(n => n.GetIPProperties().GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork))
                .SelectMany(n => n.GetIPProperties().UnicastAddresses.Select(u => u.Address))
                .ToList();
        }
        catch (NetworkInformationException) { return Array.Empty<IPAddress>(); }
    }

    /// <summary>Asks the Windows network list: category 0 is Public, 1 Private, 2 Domain. Treats "cannot tell" as public, to be safe.</summary>
    private static bool IsAnyConnectionPublic()
    {
        try
        {
            var type = Type.GetTypeFromCLSID(new Guid("DCB00C01-570F-4A9B-8D69-199FDBA5723B"));
            if (type is null) return true;
            dynamic manager = Activator.CreateInstance(type)!;
            // Judge the connection(s) that reach the internet; virtual adapters (VPN, Hyper-V, loopback) are often "Public" and must not block Drop.
            var categories = new List<int>();
            var anyConnected = new List<int>();
            foreach (dynamic connection in manager.GetNetworkConnections())
            {
                if (!(bool)connection.IsConnected) continue;
                var category = (int)connection.GetNetwork().GetCategory();
                anyConnected.Add(category);
                if ((bool)connection.IsConnectedToInternet) categories.Add(category);
            }
            var relevant = categories.Count > 0 ? categories : anyConnected;
            return relevant.Count == 0 || relevant.Contains(0);
        }
        catch (Exception) { return true; } // COM failures vary widely; never open the server when the network type is unknown
    }
}
