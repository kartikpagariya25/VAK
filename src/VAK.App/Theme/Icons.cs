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
        // widgets, batch 2
        ["ruler"] = 0xf67c, ["scale"] = 0xea50, ["temperature"] = 0xf78f, ["database"] = 0xe466, ["speed"] = 0xed9b,
        ["partly-day"] = 0xf880, ["partly-night"] = 0xf883, ["moon"] = 0xf87d, ["sunny"] = 0xf8a1, ["cloudy"] = 0xf86e,
        ["rain"] = 0xf886, ["snow"] = 0xf89b, ["storm"] = 0xf8a4, ["fog"] = 0xf874, ["sunrise"] = 0xee4f, ["sunset"] = 0xee52,
        ["clipboard"] = 0xf2c9, ["note"] = 0xf56b, ["link"] = 0xf4e4, ["message"] = 0xf7cb, ["chart"] = 0xf33f,
        ["folder"] = 0xf418, ["upload"] = 0xf1a4, ["share"] = 0xf5ea, ["bot"] = 0xe1b8, ["copy"] = 0xf32b, ["star"] = 0xf70f,
        ["open"] = 0xf582, ["save"] = 0xf67f, ["document"] = 0xf378, ["image"] = 0xf488, ["swap"] = 0xf18d,
        ["location"] = 0xf4f8, ["send"] = 0xf699, ["sync"] = 0xf190, ["calendar"] = 0xf0284, ["download"] = 0xf150,
        ["add-circle"] = 0xf10c, ["folder-open"] = 0xf42e, ["tag"] = 0xf77c,
    };

    public static IEnumerable<string> Keys => Map.Keys;

    /// <summary>Returns the glyph string for an icon key, or an empty string when unknown.</summary>
    public static string Glyph(string iconKey) =>
        Map.TryGetValue(iconKey, out var cp) ? char.ConvertFromUtf32(cp) : "";
}
