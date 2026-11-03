using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using VAK.Core.Drop;

namespace VAK.App.Services;

public enum NetworkState { Private, Public, NoPrivateAddress }

/// <summary>Decides whether Drop may run. The address Drop would listen on must belong to an adapter whose Windows network is
/// marked Private or Domain; a Public network (cafe, hotel, airport) or an unknown one is refused.</summary>
public static class NetworkCategory
{
    public static (NetworkState State, IPAddress? Address) Check()
    {
        var candidates = Candidates();
        if (candidates.Count == 0) return (NetworkState.NoPrivateAddress, null);

        var categories = AdapterCategories(); // adapter id -> 0 public, 1 private, 2 domain
        foreach (var (adapterId, address) in candidates)
            if (categories.TryGetValue(adapterId, out var category) && category != 0) return (NetworkState.Private, address);
        return (NetworkState.Public, null); // private addresses exist, but none sits on a network Windows trusts
    }

    /// <summary>Connected Wi-Fi/Ethernet adapters with a gateway, paired with their private IPv4 address.</summary>
    private static List<(string AdapterId, IPAddress Address)> Candidates()
    {
        var list = new List<(string, IPAddress)>();
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType is not (NetworkInterfaceType.Wireless80211 or NetworkInterfaceType.Ethernet)) continue;
                var props = nic.GetIPProperties();
                if (!props.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork)) continue;
                var address = PrivateNetwork.Pick(props.UnicastAddresses.Select(u => u.Address));
                if (address is not null) list.Add((Normalize(nic.Id), address));
            }
        }
        catch (NetworkInformationException) { }
        return list;
    }

    private static string Normalize(string id) => id.Trim('{', '}').ToUpperInvariant();

    private static Dictionary<string, int> AdapterCategories()
    {
        var result = new Dictionary<string, int>();
        try
        {
            var type = Type.GetTypeFromCLSID(new Guid("DCB00C01-570F-4A9B-8D69-199FDBA5723B"));
            if (type is null) return result;
            dynamic manager = Activator.CreateInstance(type)!;
            foreach (dynamic connection in manager.GetNetworkConnections())
            {
                if (!(bool)connection.IsConnected) continue;
                var adapter = Normalize(((Guid)connection.GetAdapterId()).ToString());
                result[adapter] = (int)connection.GetNetwork().GetCategory();
            }
        }
        catch (Exception) { result.Clear(); } // COM failures vary widely; "unknown" means Drop stays off
        return result;
    }
}
