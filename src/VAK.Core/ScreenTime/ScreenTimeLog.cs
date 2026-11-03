using System.Globalization;
using VAK.Core.Storage;

namespace VAK.Core.ScreenTime;

public class UsageRow
{
    public string Date { get; set; } = "";   // yyyy-MM-dd
    public string App { get; set; } = "";    // stable key, for example the process name
    public string Name { get; set; } = "";   // display name
    public double Seconds { get; set; }
}

public record AppShare(string App, string Name, double Seconds, int Percent);
public record DayTotal(DateTime Date, double Seconds);

public static class DurationFormatter
{
    /// <summary>"0m", "&lt;1m", "6m", "1h" or "6h 53m".</summary>
    public static string Short(double seconds)
    {
        if (seconds <= 0) return "0m";
        if (seconds < 60) return "<1m";
        var minutes = (int)(seconds / 60);
        if (minutes < 60) return $"{minutes}m";
        var h = minutes / 60;
        var m = minutes % 60;
        return m == 0 ? $"{h}h" : $"{h}h {m}m";
    }
}

/// <summary>Seconds of foreground time per app per day. Adds stay in memory; <see cref="Save"/> writes them out
/// (and drops anything older than 60 days).</summary>
public class ScreenTimeLog
{
    private const int KeepDays = 60;
    private readonly JsonListStore<UsageRow> _store;
    private readonly Func<DateTime> _now;
    private readonly List<UsageRow> _rows;

    public ScreenTimeLog(string path, Func<DateTime> now)
    {
        _store = new JsonListStore<UsageRow>(path);
        _now = now;
        _rows = _store.Load();
    }

    private static string Key(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public void Add(string app, string name, double seconds)
    {
        if (seconds <= 0 || string.IsNullOrWhiteSpace(app)) return;
        var date = Key(_now());
        var row = _rows.FirstOrDefault(r => r.Date == date && r.App == app);
        if (row is null) _rows.Add(new UsageRow { Date = date, App = app, Name = name, Seconds = seconds });
        else { row.Seconds += seconds; if (!string.IsNullOrWhiteSpace(name)) row.Name = name; }
    }

    public void Save()
    {
        var cutoff = Key(_now().AddDays(-KeepDays));
        _rows.RemoveAll(r => string.CompareOrdinal(r.Date, cutoff) < 0);
        _store.Save(_rows);
    }

    public double TotalSeconds(DateTime day) => _rows.Where(r => r.Date == Key(day)).Sum(r => r.Seconds);

    public int AppsUsed(DateTime day) => _rows.Count(r => r.Date == Key(day) && r.Seconds > 0);

    public IReadOnlyList<AppShare> TopApps(DateTime day, int count)
    {
        var rows = _rows.Where(r => r.Date == Key(day)).ToList();
        var total = rows.Sum(r => r.Seconds);
        return rows.OrderByDescending(r => r.Seconds).ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase).Take(count)
            .Select(r => new AppShare(r.App, r.Name, r.Seconds, total <= 0 ? 0 : (int)Math.Round(100 * r.Seconds / total))).ToList();
    }

    /// <summary>The seven days ending today, oldest first, with zeros for days without data.</summary>
    public IReadOnlyList<DayTotal> Last7Days()
    {
        var today = _now().Date;
        return Enumerable.Range(0, 7).Select(i => today.AddDays(i - 6)).Select(d => new DayTotal(d, TotalSeconds(d))).ToList();
    }

    /// <summary>Average over the days (of the last seven) that have any usage.</summary>
    public double AverageSeconds()
    {
        var active = Last7Days().Where(d => d.Seconds > 0).ToList();
        return active.Count == 0 ? 0 : active.Average(d => d.Seconds);
    }

    public double RemainingSeconds(DateTime day, int limitMinutes) => Math.Max(0, limitMinutes * 60.0 - TotalSeconds(day));
}
