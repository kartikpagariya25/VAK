using Microsoft.Win32;

namespace VAK.Core.Startup;

/// <summary>Start-with-Windows via a per-user Run key value. The key path is injectable so tests never touch the real Run key.</summary>
public class StartupRegistry
{
    public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly string _subKey;
    private readonly string _valueName;
    private readonly string _exePath;

    public StartupRegistry(string subKey, string valueName, string exePath)
    {
        _subKey = subKey; _valueName = valueName; _exePath = exePath;
    }

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(_subKey);
            return key?.GetValue(_valueName) is string;
        }
    }

    public void Set(bool enabled)
    {
        if (enabled)
        {
            using var key = Registry.CurrentUser.CreateSubKey(_subKey);
            key.SetValue(_valueName, "\"" + _exePath + "\"");
        }
        else
        {
            using var key = Registry.CurrentUser.OpenSubKey(_subKey, true);
            key?.DeleteValue(_valueName, false);
        }
    }
}
