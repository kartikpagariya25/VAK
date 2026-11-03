using System.IO;
using System.Windows.Threading;
using VAK.Core.Planner;

namespace VAK.App.Services;

/// <summary>The calendar for the whole app: the saved events and the reminder clock. Reminders keep working with no page open.</summary>
public sealed class CalendarService
{
    private static readonly TimeSpan MaxLate = TimeSpan.FromMinutes(15);

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(20) };
    private readonly FiredReminders _fired;
    private DateTime _lastCheck = DateTime.Now; // reminders from before VAK started are not shown

    public CalendarStore Store { get; }

    /// <summary>Raised after any event is added, changed or deleted.</summary>
    public event Action? Changed;

    /// <summary>Raised for each reminder that is due: the occurrence and the lead time in minutes.</summary>
    public event Action<Occurrence>? Reminder;

    public CalendarService(string dataDir)
    {
        Store = new CalendarStore(Path.Combine(dataDir, "calendar.json"));
        _fired = new FiredReminders(Path.Combine(dataDir, "calendar-fired.json"));
        _timer.Tick += (_, _) => Check();
        _timer.Start();
    }

    public CalendarEvent Add(CalendarEvent e) { var added = Store.Add(e); Changed?.Invoke(); return added; }
    public void Update(string id, Action<CalendarEvent> change) { Store.Update(id, change); Changed?.Invoke(); }
    public void Delete(string id) { Store.Delete(id); Changed?.Invoke(); }

    private void Check()
    {
        var now = DateTime.Now;
        var due = ReminderScheduler.Due(Store.All, now, _lastCheck, _fired.Keys, MaxLate);
        _lastCheck = now;
        if (due.Count == 0) return;
        foreach (var o in due) _fired.Add(ReminderScheduler.Key(o), now);
        _fired.Save(now);
        foreach (var o in due) Reminder?.Invoke(o);
    }
}
