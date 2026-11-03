namespace VAK.Core.Hotkeys;

public static class HotkeyParser
{
    public const uint Alt = 1, Ctrl = 2, Shift = 4, Win = 8;

    /// <summary>Parses text such as "Ctrl+Alt+Space" into Win32 modifier flags and a virtual-key code.
    /// Returns null unless at least one modifier and exactly one known key are present.</summary>
    public static (uint Modifiers, uint VirtualKey)? TryParse(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        uint mods = 0;
        uint? key = null;
        foreach (var raw in text.Split('+', StringSplitOptions.TrimEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "ctrl" or "control": mods |= Ctrl; continue;
                case "alt": mods |= Alt; continue;
                case "shift": mods |= Shift; continue;
                case "win" or "windows": mods |= Win; continue;
            }
            if (key is not null) return null;
            key = KeyCode(raw);
            if (key is null) return null;
        }
        return mods != 0 && key is not null ? (mods, key.Value) : null;
    }

    private static uint? KeyCode(string name)
    {
        if (name.Length == 1 && char.IsAsciiLetterOrDigit(name[0])) return char.ToUpperInvariant(name[0]);
        if (name.StartsWith('F') && int.TryParse(name.AsSpan(1), out var n) && n is >= 1 and <= 12) return (uint)(0x6F + n);
        return name.ToLowerInvariant() switch
        {
            "space" => 0x20, "enter" => 0x0D, "tab" => 0x09, "esc" or "escape" => 0x1B,
            _ => null,
        };
    }
}
