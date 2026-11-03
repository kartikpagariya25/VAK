namespace VAK.Core.Timers;

public static class TimerFormatter
{
    /// <summary>Formats a remaining time as "25:00". Partial seconds round up so a timer never shows 00:00 before it is done.</summary>
    public static string Mmss(TimeSpan t)
    {
        if (t <= TimeSpan.Zero) return "00:00";
        var seconds = (int)Math.Ceiling(t.TotalSeconds);
        return $"{seconds / 60:00}:{seconds % 60:00}";
    }
}
