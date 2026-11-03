using System.Text.RegularExpressions;

namespace VAK.Core.Message;

public enum BannerAnimation { Slide, Fade, TypeOn, Scroll, Blur, Pulse }
public enum BannerFinish { Solid, Silver, Ocean, Aurora }

public static class BannerPalette
{
    /// <summary>The seven quick colours offered on the Message page (white, cream, amber, coral, pink, violet, sky).</summary>
    public static IReadOnlyList<string> Colors { get; } =
        new[] { "#FFFFFF", "#FFE0C2", "#F5A623", "#EF6C4D", "#F43F6E", "#A855F7", "#4FB8FF" };
}

/// <summary>A message to flash on the notch. Use <see cref="TryCreate"/> so text, speed, duration and colour are always valid.</summary>
public record BannerSpec(string Text, BannerAnimation Animation, double Speed, string ColorHex, BannerFinish Finish, int DurationSeconds, bool Repeat)
{
    public const int MaxText = 60;
    private static readonly Regex Hex = new("^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);

    /// <summary>Returns null for blank text. Text is trimmed and cut to 60 characters, speed is kept within 0.5-2,
    /// duration within 1-60 seconds, and an invalid colour becomes white.</summary>
    public static BannerSpec? TryCreate(string text, BannerAnimation animation, double speed, string colorHex, BannerFinish finish, int seconds, bool repeat)
    {
        var t = text?.Trim() ?? "";
        if (t.Length == 0) return null;
        if (t.Length > MaxText) t = t[..MaxText];
        var color = colorHex is not null && Hex.IsMatch(colorHex) ? colorHex.ToUpperInvariant() : "#FFFFFF";
        return new BannerSpec(t, animation, Math.Clamp(speed, 0.5, 2), color, finish, Math.Clamp(seconds, 1, 60), repeat);
    }
}
