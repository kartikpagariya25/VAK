using System.ComponentModel;
using System.Diagnostics;
using VAK.Core.Links;

namespace VAK.App.Services;

public static class Launcher
{
    /// <summary>Opens an http(s) address in the default browser. Anything else is refused, and failures are swallowed.</summary>
    public static bool OpenUrl(string url)
    {
        var safe = LinkNormalizer.Normalize(url);
        if (safe is null) return false;
        try
        {
            Process.Start(new ProcessStartInfo(safe) { UseShellExecute = true });
            return true;
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException) { return false; }
    }
}
