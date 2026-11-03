namespace VAK.Core.Settings;

public class AppSettings
{
    public string Hotkey { get; set; } = "Ctrl+Alt+Space";
    public bool StartWithWindows { get; set; }
    public bool Use24Hour { get; set; }
    public string Theme { get; set; } = "dark";
    public string?[] HomeSlots { get; set; } = new string?[6];
    public bool HomeInitialized { get; set; }
    public List<string> Dock { get; set; } = new() { "system", "clock", "timers" };
    public ClockSettings Clock { get; set; } = new();
    public TimerSettings Timers { get; set; } = new();
    public List<WorldClockEntry> WorldClocks { get; set; } = new();
    public bool WorldClocksInitialized { get; set; }
    public int WaterToday { get; set; }
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
}

public record WorldClockEntry(string Label, string TimeZoneId);
