using System.Globalization;

namespace VAK.Core.Clock;

public static class ClockFormatter
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Time(DateTimeOffset now, TimeZoneInfo tz, bool use24Hour, bool showSeconds)
    {
        var local = TimeZoneInfo.ConvertTime(now, tz);
        var format = use24Hour
            ? (showSeconds ? "HH:mm:ss" : "HH:mm")
            : (showSeconds ? "h:mm:ss tt" : "h:mm tt");
        return local.ToString(format, Inv);
    }

    public static string Date(DateTimeOffset now, string style) => now.ToString(style switch
    {
        "short" => "ddd, MMM d",
        "compact" => "M/d",
        _ => "dddd, MMMM d, yyyy",
    }, Inv);

    /// <summary>Home-page header subtitle, for example "Tuesday, Nov 3".</summary>
    public static string HeaderDate(DateTimeOffset now) => now.ToString("dddd, MMM d", Inv);

    public static string OffsetLabel(DateTimeOffset now, TimeZoneInfo tz, TimeZoneInfo local)
    {
        var diff = tz.GetUtcOffset(now) - local.GetUtcOffset(now);
        var totalMinutes = (int)Math.Round(diff.TotalMinutes);
        if (totalMinutes == 0) return "";
        var sign = totalMinutes < 0 ? "-" : "+";
        var abs = Math.Abs(totalMinutes);
        var parts = new List<string>();
        if (abs / 60 > 0) parts.Add($"{abs / 60}h");
        if (abs % 60 > 0) parts.Add($"{abs % 60}m");
        return sign + string.Join(" ", parts);
    }

    public static TimeZoneInfo? TryFind(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException) { return null; }
    }
}
