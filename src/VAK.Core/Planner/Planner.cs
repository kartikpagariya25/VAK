using System.Text.RegularExpressions;
using VAK.Core.Storage;

namespace VAK.Core.Planner;

public enum Repeat { None, Daily, Weekly, Monthly, Yearly }

public class CalendarEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";
    /// <summary>Start date and time. For all-day events only the date matters.</summary>
    public DateTime Start { get; set; }
    /// <summary>End date and time. For all-day events this is the last day, inclusive.</summary>
    public DateTime End { get; set; }
    public bool AllDay { get; set; }
    public string Color { get; set; } = "#2F7CF6";
    public string Note { get; set; } = "";
    public Repeat Repeat { get; set; }
    /// <summary>Last day the series may start on (inclusive); null repeats forever.</summary>
    public DateTime? RepeatUntil { get; set; }
    /// <summary>Minutes before the start to remind; 0 means at the start; -1 means no reminder.</summary>
    public int ReminderMinutes { get; set; } = -1;
}

/// <summary>One concrete showing of an event (a repeating event has many).</summary>
public record Occurrence(CalendarEvent Event, DateTime Start, DateTime End);

public static class Recurrence
{
    private const int MaxSteps = 200_000;

    /// <summary>All occurrences that touch any day from <paramref name="from"/> to <paramref name="to"/> (dates, inclusive).
    /// An occurrence touches every day from its start date to its end date, so overnight and multi-day events show on each day.</summary>
    public static IEnumerable<Occurrence> Between(CalendarEvent e, DateTime from, DateTime to)
    {
        var f = from.Date; var t = to.Date;
        var length = e.End > e.Start ? e.End - e.Start : TimeSpan.Zero;
        var lengthDays = (int)Math.Ceiling(length.TotalDays);

        for (var n = FirstStep(e, f, lengthDays); n < MaxSteps; n++)
        {
            var start = StartOf(e, n);
            if (start.Date > t) yield break;
            if (e.RepeatUntil is { } until && start.Date > until.Date) yield break;
            var end = start + length;
            if (end.Date >= f) yield return new Occurrence(e, start, end);
            if (e.Repeat == Repeat.None) yield break;
        }
    }

    private static DateTime StartOf(CalendarEvent e, int n) => e.Repeat switch
    {
        Repeat.Daily => e.Start.AddDays(n),
        Repeat.Weekly => e.Start.AddDays(7 * n),
        Repeat.Monthly => e.Start.AddMonths(n),   // AddMonths/AddYears clamp (31st -> 30th/28th, Feb 29 -> Feb 28)
        Repeat.Yearly => e.Start.AddYears(n),
        _ => e.Start,
    };

    /// <summary>Where to begin counting so a query far in the future does not walk every earlier occurrence.</summary>
    private static int FirstStep(CalendarEvent e, DateTime from, int lengthDays)
    {
        var daysAway = (from - e.Start.Date).Days - lengthDays - 1;
        if (daysAway <= 0) return 0;
        return e.Repeat switch
        {
            Repeat.Daily => daysAway,
            Repeat.Weekly => daysAway / 7,
            Repeat.Monthly => Math.Max(0, daysAway / 31 - 1),
            Repeat.Yearly => Math.Max(0, daysAway / 366 - 1),
            _ => 0,
        };
    }
}

/// <summary>The saved events. Every change is saved straight away.</summary>
public class CalendarStore
{
    private readonly JsonListStore<CalendarEvent> _store;
    private readonly List<CalendarEvent> _events;

    public CalendarStore(string path)
    {
        _store = new JsonListStore<CalendarEvent>(path);
        _events = _store.Load();
    }

    public IReadOnlyList<CalendarEvent> All => _events.ToList();

    public CalendarEvent Add(CalendarEvent e)
    {
        _events.Add(e);
        _store.Save(_events);
        return e;
    }

    public void Update(string id, Action<CalendarEvent> change)
    {
        var e = _events.FirstOrDefault(x => x.Id == id);
        if (e is null) return;
        change(e);
        _store.Save(_events);
    }

    public void Delete(string id)
    {
        if (_events.RemoveAll(e => e.Id == id) > 0) _store.Save(_events);
    }

    /// <summary>Everything that touches a day: all-day events first, then by start time.</summary>
    public IReadOnlyList<Occurrence> EventsOn(DateTime day) =>
        _events.SelectMany(e => Recurrence.Between(e, day, day))
            .OrderBy(o => o.Event.AllDay ? 0 : 1).ThenBy(o => o.Start).ThenBy(o => o.Event.Title, StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>For each day of a month that has events, the colours to show as dots (at most three, no repeats).</summary>
    public Dictionary<int, List<string>> DaysWithEvents(int year, int month)
    {
        var first = new DateTime(year, month, 1);
        var last = first.AddMonths(1).AddDays(-1);
        var result = new Dictionary<int, List<string>>();
        foreach (var o in _events.SelectMany(e => Recurrence.Between(e, first, last)))
            for (var d = o.Start.Date; d <= o.End.Date; d = d.AddDays(1))
            {
                if (d < first || d > last) continue;
                if (!result.TryGetValue(d.Day, out var colors)) result[d.Day] = colors = new List<string>();
                if (colors.Count < 3 && !colors.Contains(o.Event.Color)) colors.Add(o.Event.Color);
            }
        return result;
    }

    /// <summary>The next occurrences from <paramref name="now"/> within <paramref name="days"/> days, soonest first.</summary>
    public IReadOnlyList<Occurrence> Upcoming(DateTime now, int days, int count) =>
        _events.SelectMany(e => Recurrence.Between(e, now.Date, now.Date.AddDays(days)))
            .Where(o => o.Event.AllDay ? o.End.Date >= now.Date : o.End > now)
            .OrderBy(o => o.Start).ThenBy(o => o.Event.Title, StringComparer.OrdinalIgnoreCase).Take(count).ToList();
}

public static class ReminderScheduler
{
    public static string Key(Occurrence o) => $"{o.Event.Id}|{o.Start.Ticks}";

    /// <summary>Occurrences whose reminder time fell inside (lastCheck, now]. A reminder missed by more than
    /// <paramref name="maxLate"/> (the PC was asleep or off) is skipped, never shown hours late. Each one is reported once:
    /// its key is added to <paramref name="fired"/>.</summary>
    public static List<Occurrence> Due(IEnumerable<CalendarEvent> events, DateTime now, DateTime lastCheck, ISet<string> fired, TimeSpan maxLate)
    {
        var windowStart = lastCheck > now - maxLate ? lastCheck : now - maxLate;
        var due = new List<Occurrence>();
        foreach (var e in events.Where(x => x.ReminderMinutes >= 0))
        {
            var lead = TimeSpan.FromMinutes(e.ReminderMinutes);
            foreach (var o in Recurrence.Between(e, windowStart.Date, (now + lead).Date))
            {
                var at = (o.Event.AllDay ? o.Start.Date.AddHours(9) : o.Start) - lead; // all-day events remind at 09:00
                if (at > windowStart && at <= now && fired.Add(Key(o))) due.Add(o);
            }
        }
        return due.OrderBy(o => o.Start).ToList();
    }
}

public class FiredEntry
{
    public string Key { get; set; } = "";
    public DateTime At { get; set; }
}

/// <summary>Which reminders were already shown, so they are not shown again after a restart. Entries older than two days are dropped.</summary>
public class FiredReminders
{
    private readonly JsonListStore<FiredEntry> _store;
    private readonly List<FiredEntry> _entries;

    public FiredReminders(string path)
    {
        _store = new JsonListStore<FiredEntry>(path);
        _entries = _store.Load();
        Keys = _entries.Select(e => e.Key).ToHashSet();
    }

    /// <summary>Pass this to <see cref="ReminderScheduler.Due"/>; use <see cref="Add"/> to record when each was shown.</summary>
    public HashSet<string> Keys { get; }

    public bool Contains(string key) => Keys.Contains(key);

    public void Add(string key, DateTime at)
    {
        Keys.Add(key);
        if (_entries.All(e => e.Key != key)) _entries.Add(new FiredEntry { Key = key, At = at });
    }

    public void Save(DateTime now)
    {
        _entries.RemoveAll(e => e.At < now.AddDays(-2));
        _store.Save(_entries);
    }
}

public static class MonthGrid
{
    /// <summary>The 42 days (six weeks) shown for a month, starting on <paramref name="firstDay"/>, including the days of the neighbouring months.</summary>
    public static List<DateTime> Build(int year, int month, DayOfWeek firstDay)
    {
        var first = new DateTime(year, month, 1);
        var offset = ((int)first.DayOfWeek - (int)firstDay + 7) % 7;
        var start = first.AddDays(-offset);
        return Enumerable.Range(0, 42).Select(i => start.AddDays(i)).ToList();
    }
}

public static class TimeInput
{
    private static readonly Regex Pattern = new(@"^(\d{1,2})(?::?(\d{2}))?\s*(am|pm)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Reads times people type: "9:30", "930", "9", "9pm", "9:30 PM". Returns null for anything that is not a time.</summary>
    public static TimeSpan? TryParse(string? text)
    {
        var m = Pattern.Match((text ?? "").Trim());
        if (!m.Success) return null;
        var hour = int.Parse(m.Groups[1].Value);
        var minute = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 0;
        if (minute > 59) return null;
        if (m.Groups[3].Success)
        {
            if (hour is < 1 or > 12) return null;
            hour %= 12;
            if (m.Groups[3].Value.Equals("pm", StringComparison.OrdinalIgnoreCase)) hour += 12;
        }
        else if (hour > 23) return null;
        return new TimeSpan(hour, minute, 0);
    }
}
