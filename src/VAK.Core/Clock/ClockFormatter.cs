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

    private static readonly Dictionary<string, (string Standard, string Daylight)> Abbreviations = new()
    {
        ["India Standard Time"] = ("IST", "IST"),
        ["Eastern Standard Time"] = ("EST", "EDT"),
        ["Central Standard Time"] = ("CST", "CDT"),
        ["Mountain Standard Time"] = ("MST", "MDT"),
        ["Pacific Standard Time"] = ("PST", "PDT"),
        ["GMT Standard Time"] = ("GMT", "BST"),
        ["Greenwich Standard Time"] = ("GMT", "GMT"),
        ["UTC"] = ("UTC", "UTC"),
        ["Tokyo Standard Time"] = ("JST", "JST"),
        ["Korea Standard Time"] = ("KST", "KST"),
        ["Singapore Standard Time"] = ("SGT", "SGT"),
        ["W. Europe Standard Time"] = ("CET", "CEST"),
        ["Central Europe Standard Time"] = ("CET", "CEST"),
        ["Romance Standard Time"] = ("CET", "CEST"),
        ["AUS Eastern Standard Time"] = ("AEST", "AEDT"),
    };

    /// <summary>Short zone name such as "IST" or "EDT". Windows has no abbreviations, so known zones come from a table
    /// and others fall back to the initials of the zone's standard name.</summary>
    public static string Abbreviation(TimeZoneInfo tz, DateTimeOffset now)
    {
        if (Abbreviations.TryGetValue(tz.Id, out var known))
            return tz.IsDaylightSavingTime(now) ? known.Daylight : known.Standard;
        var initials = string.Concat(tz.StandardName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => char.IsLetter(w[0])).Select(w => char.ToUpperInvariant(w[0])));
        return initials.Length >= 2 ? initials : tz.Id;
    }

    /// <summary>Offset text such as "UTC+05:30".</summary>
    public static string UtcOffsetLabel(TimeZoneInfo tz, DateTimeOffset now)
    {
        var o = tz.GetUtcOffset(now);
        return (o < TimeSpan.Zero ? "UTC-" : "UTC+") + o.Duration().ToString(@"hh\:mm", Inv);
    }

    public static TimeZoneInfo? TryFind(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException) { return null; }
    }
}
