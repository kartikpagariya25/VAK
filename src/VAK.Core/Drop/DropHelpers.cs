using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace VAK.Core.Drop;

public static class PrivateNetwork
{
    /// <summary>True for IPv4 addresses in the private ranges 10/8, 172.16/12 and 192.168/16.</summary>
    public static bool IsPrivate(IPAddress ip)
    {
        if (ip.AddressFamily != AddressFamily.InterNetwork) return false;
        var b = ip.GetAddressBytes();
        return b[0] == 10 || (b[0] == 172 && b[1] is >= 16 and <= 31) || (b[0] == 192 && b[1] == 168);
    }

    /// <summary>The first private LAN address, or null when the machine has none.</summary>
    public static IPAddress? Pick(IEnumerable<IPAddress> addresses) => addresses.FirstOrDefault(IsPrivate);
}

public static class UploadNames
{
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };

    /// <summary>Turns a name sent by a browser into a safe file name: no folders, no invalid characters, no Windows device
    /// names, at most 120 characters. Never returns an empty name.</summary>
    public static string Safe(string? name)
    {
        var text = name ?? "";
        text = text[(Math.Max(text.LastIndexOf('/'), text.LastIndexOf('\\')) + 1)..];
        var invalid = Path.GetInvalidFileNameChars();
        text = new string(text.Where(c => !invalid.Contains(c) && !char.IsControl(c)).ToArray()).Trim().Trim('.').Trim();
        if (text.Length == 0) return "file";

        var dot = text.IndexOf('.');
        var stem = dot < 0 ? text : text[..dot];
        var rest = dot < 0 ? "" : text[dot..];
        if (Reserved.Contains(stem)) stem = "file";
        var ext = rest.Length > 20 ? rest[..20] : rest;
        if (stem.Length + ext.Length > 120) stem = stem[..Math.Max(1, 120 - ext.Length)];
        return stem + ext;
    }
}

public static class DropToken
{
    /// <summary>A random 128-bit token as 32 lowercase hex characters. It is part of the share link, so only people with the QR code can connect.</summary>
    public static string New() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    /// <summary>Compares in constant time; null or empty never matches.</summary>
    public static bool Matches(string expected, string? given)
    {
        if (string.IsNullOrEmpty(given)) return false;
        var a = Encoding.UTF8.GetBytes(expected);
        var b = Encoding.UTF8.GetBytes(given);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }
}
