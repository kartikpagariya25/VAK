namespace VAK.Core.Settings;

public enum NotchMode { Full, Minimal, Hidden }

/// <summary>How much of the notch to show right now.</summary>
public static class NotchPolicy
{
    /// <param name="poweredOff">The notch was turned off with its power button. That lasts for this run only; the saved settings are untouched.</param>
    public static NotchMode Decide(NotchSettings n, bool fullscreenApp, bool maximizedApp, bool poweredOff = false)
    {
        if (poweredOff || !n.ShowNotch || n.Visibility == "hidden") return NotchMode.Hidden;
        if (fullscreenApp && n.HideInFullscreen) return NotchMode.Hidden;
        return n.Visibility switch
        {
            "minimal" => NotchMode.Minimal,
            "smart" => maximizedApp ? NotchMode.Minimal : NotchMode.Full,
            _ => NotchMode.Full,
        };
    }
}

public record NotchShape(double Width, double Height, double TopGap, double BottomRadius, double TopRadius);

/// <summary>The resting size and corners of the notch for each style, scaled by the Size setting (70% to 130%).</summary>
public static class NotchGeometry
{
    public static double Scale(NotchSettings n) => Math.Clamp(n.SizePercent, 70, 130) / 100.0;

    public static NotchShape Collapsed(NotchSettings n)
    {
        var k = Scale(n);
        var width = n.Shape switch { "wide" => 260.0, "standard" => 230.0, _ => 200.0 };
        return n.Style switch
        {
            "line" => new NotchShape(Math.Round(width * 0.8 * k), Math.Round(6 * k), 0, 3 * k, 0),
            "island" => new NotchShape(Math.Round((width - 10) * k), Math.Round(32 * k), 8, 16 * k, 16 * k),
            _ => new NotchShape(Math.Round(width * k), Math.Round(34 * k), 0, 18 * k, 0),
        };
    }
}

public record ThemePalette(string Name, string Panel, string Card, string Control, string Accent, string Primary, string Text, string TextDim, string TextFaint, string Divider, bool Light);

public static class Themes
{
    public static readonly IReadOnlyList<ThemePalette> All = new[]
    {
        new ThemePalette("black", "#000000", "#0D0D0F", "#2A2A2C", "#2F7CF6", "#1D64D8", "#FFFFFF", "#A6A6AA", "#6E6E73", "#1F1F22", false),
        new ThemePalette("ember", "#000000", "#0F0C0B", "#2C2624", "#FF6A2B", "#E8500F", "#FFFFFF", "#A8A09C", "#736A66", "#221C1A", false),
        new ThemePalette("vivid", "#000000", "#0A0D12", "#222A36", "#3D8BFF", "#2F7CF6", "#FFFFFF", "#9FB0C8", "#66758C", "#18202B", false),
        new ThemePalette("silver", "#F2F2F4", "#FFFFFF", "#E4E4E8", "#2F7CF6", "#1D64D8", "#111114", "#5C5C63", "#8E8E96", "#D9D9DE", true),
    };

    public static ThemePalette Get(string? name) => All.FirstOrDefault(t => t.Name == name) ?? All[0];
}

public static class EdgeLightSpec
{
    public static double LoopSeconds(string speed) => speed switch { "slow" => 6.0, "fast" => 1.8, _ => 3.5 };
    public static double Opacity(int intensity) => Math.Clamp(intensity / 100.0, 0.1, 1.0);
    public static bool ShouldPlay(string when, bool eventActive, bool hovered) => when != "events" || eventActive || hovered;
}

public enum LiveKind { Battery, Volume, Clipboard, TimerDone, CalendarAlert, NowPlaying }

public static class LiveActivityPolicy
{
    public static bool Allows(LiveSettings l, LiveKind kind) => kind switch
    {
        LiveKind.Battery => l.Battery,
        LiveKind.Volume => l.Volume,
        LiveKind.Clipboard => l.Clipboard,
        LiveKind.TimerDone => l.TimersDone,
        LiveKind.CalendarAlert => l.CalendarAlerts,
        LiveKind.NowPlaying => l.NowPlaying,
        _ => false,
    };

    /// <summary>When the notch is tucked away, events may still grow it briefly unless the user turned that off.</summary>
    public static bool MayShowNow(NotchSettings n, NotchMode mode) => mode != NotchMode.Hidden || n.ShowEventsOverApps;

    public static bool AlsoWindowsNotice(LiveSettings l) => l.RemindersWhere == "both";

    public static string CopiedLabel(string text, bool isImage, bool hide)
    {
        if (isImage) return "Image";
        if (hide) return "Text";
        var one = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return one.Length > 28 ? one[..27] + "…" : one;
    }
}

/// <summary>Edits the dock list: which modules are in it, and in what order.</summary>
public static class DockEditor
{
    public static bool Set(List<string> dock, string id, bool on, int capacity)
    {
        var has = dock.Contains(id);
        if (on == has) return true;
        if (!on) { dock.Remove(id); return true; }
        if (dock.Count >= capacity) return false;
        dock.Add(id);
        return true;
    }

    public static bool Move(List<string> dock, string id, int delta)
    {
        var i = dock.IndexOf(id);
        var j = i + delta;
        if (i < 0 || j < 0 || j >= dock.Count) return false;
        (dock[i], dock[j]) = (dock[j], dock[i]);
        return true;
    }
}
