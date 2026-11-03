using System.Globalization;
using System.Text.Json.Serialization;

namespace VAK.Core.Settings;

public class AppSettings
{
    public string Hotkey { get; set; } = "Ctrl+Alt+Space";
    public bool StartWithWindows { get; set; }
    /// <summary>"system" (follow Windows), "12" or "24".</summary>
    public string TimeFormat { get; set; } = "system";

    /// <summary>The effective clock style; setting it chooses an explicit 12 or 24 hour format.</summary>
    [JsonIgnore]
    public bool Use24Hour
    {
        get => TimeFormat == "24" || (TimeFormat != "12" && SystemUses24Hour());
        set => TimeFormat = value ? "24" : "12";
    }

    /// <summary>Settings files from before TimeFormat existed stored a plain true/false; read it once so nobody's clock style changes.</summary>
    [JsonPropertyName("Use24Hour"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? LegacyUse24Hour
    {
        get => null;
        set { if (value is { } v && TimeFormat == "system") TimeFormat = v ? "24" : "12"; }
    }

    public static bool SystemUses24Hour() => CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern.Contains('H');
    public string Theme { get; set; } = "dark";
    public string?[] HomeSlots { get; set; } = new string?[6];
    public bool HomeInitialized { get; set; }
    public string SearchEngine { get; set; } = "google";
    public int ScreenTimeLimitMinutes { get; set; } = 420;
    public List<string> Dock { get; set; } = new() { "system", "clipboard", "notes", "timers", "fileshelf", "drop", "weather", "clock", "calendar", "units", "converter", "links", "message", "screentime", "aicoding" };
    public ClockSettings Clock { get; set; } = new();
    public TimerSettings Timers { get; set; } = new();
    public WeatherSettings Weather { get; set; } = new();
    public MessageSettings Message { get; set; } = new();
    public ConverterSettings Converter { get; set; } = new();
    public AiSettings Ai { get; set; } = new();
    public NotchSettings Notch { get; set; } = new();
    public AppearanceSettings Appearance { get; set; } = new();
    public LiveSettings Live { get; set; } = new();
    public List<WorldClockEntry> WorldClocks { get; set; } = new();
    public bool WorldClocksInitialized { get; set; }
    public int WaterToday { get; set; }
    public int PomodoroCompletedToday { get; set; }

    /// <summary>Date (yyyy-MM-dd) the daily counters belong to; they reset when it changes.</summary>
    public string CountersDate { get; set; } = "";
}

public class ClockSettings
{
    public string Style { get; set; } = "digital";
    public bool ShowSeconds { get; set; }
    public bool ShowDate { get; set; } = true;
    public string DateStyle { get; set; } = "full";
}

public class TimerSettings
{
    public int FocusMinutes { get; set; } = 25;
    public int BreakMinutes { get; set; } = 5;
    public int LongBreakMinutes { get; set; } = 15;
    public int Cycles { get; set; } = 4;
    public bool AutoStartNext { get; set; } = true;
    public int CountdownMinutes { get; set; } = 15;
}

public class AiSettings
{
    /// <summary>"claude" or "gpt".</summary>
    public string Provider { get; set; } = "claude";
    /// <summary>"web" (the real site in an embedded browser) or "api" (native chat with an API key).</summary>
    public string Mode { get; set; } = "web";
    /// <summary>The model last used for each provider in API mode.</summary>
    public Dictionary<string, string> Models { get; set; } = new();
}

public class ConverterSettings
{
    public int Quality { get; set; } = 90;
    public int MaxSide { get; set; }
}

public class MessageSettings
{
    public int Animation { get; set; }
    public double Speed { get; set; } = 1;
    public string Color { get; set; } = "#FFFFFF";
    public int Finish { get; set; }
    public int Seconds { get; set; } = 5;
    public bool Repeat { get; set; }
}

public class WeatherSettings
{
    public string City { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public bool Fahrenheit { get; set; }
    /// <summary>"kmh" or "mph".</summary>
    public string WindUnit { get; set; } = "kmh";
}

public record WorldClockEntry(string Label, string TimeZoneId);

public class NotchSettings
{
    /// <summary>"notch" (grows out of the screen edge), "line" (slim bar) or "island" (floating pill).</summary>
    public string Style { get; set; } = "notch";
    /// <summary>"compact", "standard" or "wide".</summary>
    public string Shape { get; set; } = "compact";
    public int SizePercent { get; set; } = 100;
    /// <summary>"corners" (time left, battery right) or "center".</summary>
    public string RestingLayout { get; set; } = "corners";
    /// <summary>"hover" or "click".</summary>
    public string OpensOn { get; set; } = "hover";
    /// <summary>"primary" or a monitor device name.</summary>
    public string Display { get; set; } = "primary";
    /// <summary>"always", "smart" (tucked away over maximized apps), "minimal" (a small arrow) or "hidden".</summary>
    public string Visibility { get; set; } = "always";
    public bool ShowNotch { get; set; } = true;
    public bool HideInFullscreen { get; set; } = true;
    public bool ShowEventsOverApps { get; set; } = true;
    public bool TabAnimations { get; set; } = true;
}

public class AppearanceSettings
{
    /// <summary>"black", "ember", "vivid" or "silver".</summary>
    public string Theme { get; set; } = "black";
    public bool Outline { get; set; }
    public int OutlineWidth { get; set; } = 1;
    /// <summary>"none", "subtle", "medium" or "deep".</summary>
    public string Shadow { get; set; } = "medium";
    /// <summary>"none", "low", "medium" or "high".</summary>
    public string SoftEdge { get; set; } = "medium";
    public EdgeLightSettings EdgeLight { get; set; } = new();
}

public class EdgeLightSettings
{
    public bool Enabled { get; set; } = true;
    /// <summary>"comet", "twin" or "breathe".</summary>
    public string Style { get; set; } = "twin";
    public string Color { get; set; } = "#FFFFFF";
    public int Intensity { get; set; } = 80;
    /// <summary>"slow", "normal" or "fast".</summary>
    public string Speed { get; set; } = "normal";
    /// <summary>"always" or "events" (events and hover).</summary>
    public string When { get; set; } = "always";
}

public class LiveSettings
{
    public bool NowPlaying { get; set; } = true;
    public bool BrowserMedia { get; set; } = true;
    /// <summary>"auto" (from the album art) or a colour like #FF3B30.</summary>
    public string MusicColor { get; set; } = "auto";
    public bool Battery { get; set; } = true;
    public bool Volume { get; set; } = true;
    public bool Clipboard { get; set; } = true;
    public bool HideCopiedText { get; set; } = true;
    public bool RunningTimers { get; set; } = true;
    public bool TimersDone { get; set; } = true;
    public bool CalendarAlerts { get; set; } = true;
    /// <summary>"notch" or "both" (notch and a Windows notice).</summary>
    public string RemindersWhere { get; set; } = "notch";
    /// <summary>2, 3 or 5.</summary>
    public int EventSeconds { get; set; } = 3;
}
