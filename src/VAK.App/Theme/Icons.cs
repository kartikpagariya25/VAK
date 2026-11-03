namespace VAK.App.Theme;

/// <summary>Maps VAK icon keys to Fluent System Icons (20px regular) codepoints.</summary>
public static class Icons
{
    private static readonly Dictionary<string, int> Map = new()
    {
        ["home"] = 0xf480, ["system"] = 0xe4ac, ["clock"] = 0xf2dd, ["timers"] = 0xed88,
        ["settings"] = 0xf6a9, ["close"] = 0xf369, ["pin"] = 0xf601, ["add"] = 0xf109,
        ["play"] = 0xf605, ["pause"] = 0xf5a1, ["battery"] = 0xe143, ["charge"] = 0xe618,
        ["wifi"] = 0xf8ac, ["volume"] = 0xeb42, ["mute"] = 0xeb4a, ["reset"] = 0xf13f,
        ["next"] = 0xf569, ["search"] = 0xf68f, ["up"] = 0xf19b, ["down"] = 0xf148,
        ["water"] = 0xe590, ["edit"] = 0xf3dd, ["chevron"] = 0xf2a3, ["undo"] = 0xf199,
        ["check"] = 0xf294, ["minus"] = 0xebd0, ["globe"] = 0xf45a, ["hourglass"] = 0xef6b,
        ["trash"] = 0xf34c,
    };

    public static IEnumerable<string> Keys => Map.Keys;

    /// <summary>Returns the glyph string for an icon key, or an empty string when unknown.</summary>
    public static string Glyph(string iconKey) =>
        Map.TryGetValue(iconKey, out var cp) ? char.ConvertFromUtf32(cp) : "";
}
