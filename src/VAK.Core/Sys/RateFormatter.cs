using System.Globalization;

namespace VAK.Core.Sys;

public static class RateFormatter
{
    /// <summary>Formats a byte rate as "9 KB/s" or "1.5 MB/s".</summary>
    public static string Format(double bytesPerSecond)
    {
        var inv = CultureInfo.InvariantCulture;
        var kb = bytesPerSecond / 1024;
        if (kb < 1000) return Math.Floor(kb).ToString("0", inv) + " KB/s";
        return (kb / 1024).ToString("0.#", inv) + " MB/s";
    }
}
