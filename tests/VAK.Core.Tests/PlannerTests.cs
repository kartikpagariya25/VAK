using VAK.Core.Planner;
using Xunit;

namespace VAK.Core.Tests;

public class PlannerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vak-cal-" + Guid.NewGuid().ToString("N"));
    public PlannerTests() => Directory.CreateDirectory(_dir);
    public void Dispose() => Directory.Delete(_dir, true);

    private static CalendarEvent Ev(string title, DateTime start, DateTime? end = null, bool allDay = false,
        Repeat repeat = Repeat.None, DateTime? until = null, int reminder = -1) =>
        new() { Title = title, Start = start, End = end ?? start.AddHours(1), AllDay = allDay, Repeat = repeat, RepeatUntil = until, ReminderMinutes = reminder };

    private static DateTime D(int m, int d, int h = 0, int min = 0, int y = 2026) => new(y, m, d, h, min, 0);
    private static List<DateTime> Starts(CalendarEvent e, DateTime from, DateTime to) =>
        Recurrence.Between(e, from, to).Select(o => o.Start).ToList();

    // ---- recurrence
    [Fact]
    public void Single_EventAppearsOnlyInsideItsRange()
    {
        var e = Ev("once", D(11, 3, 9));
        Assert.Single(Recurrence.Between(e, D(11, 1), D(11, 30)));
        Assert.Empty(Recurrence.Between(e, D(11, 4), D(11, 30)));
        Assert.Empty(Recurrence.Between(e, D(10, 1), D(11, 2)));
    }

    [Fact]
    public void Daily_RepeatsEveryDay() =>
        Assert.Equal(new[] { D(11, 5, 9), D(11, 6, 9), D(11, 7, 9) }, Starts(Ev("d", D(11, 3, 9), repeat: Repeat.Daily), D(11, 5), D(11, 7)));

    [Fact]
    public void Weekly_RepeatsOnTheSameWeekday() =>
        Assert.Equal(new[] { D(11, 3, 9), D(11, 10, 9), D(11, 17, 9), D(11, 24, 9) }, Starts(Ev("w", D(11, 3, 9), repeat: Repeat.Weekly), D(11, 1), D(11, 30)));

    [Fact]
    public void Monthly_OnThe31st_FallsBackToTheLastDayOfShortMonths() =>
        Assert.Equal(new[] { D(1, 31, 8), D(2, 28, 8), D(3, 31, 8), D(4, 30, 8) }, Starts(Ev("m", D(1, 31, 8), repeat: Repeat.Monthly), D(1, 1), D(4, 30)));

    [Fact]
    public void Yearly_OnFeb29_UsesFeb28InNonLeapYears() =>
        Assert.Equal(new[] { D(2, 28, 9, 0, 2025) }, Starts(Ev("y", D(2, 29, 9, 0, 2024), repeat: Repeat.Yearly), D(1, 1, 0, 0, 2025), D(12, 31, 0, 0, 2025)));

    [Fact]
    public void RepeatUntil_IsInclusive_AndStopsTheSeries() =>
        Assert.Equal(new[] { D(11, 3, 9), D(11, 4, 9), D(11, 5, 9) }, Starts(Ev("d", D(11, 3, 9), repeat: Repeat.Daily, until: D(11, 5)), D(11, 1), D(11, 30)));

    [Fact]
    public void RepeatingEvent_NeverAppearsBeforeItStarts() =>
        Assert.Empty(Recurrence.Between(Ev("d", D(11, 10, 9), repeat: Repeat.Daily), D(11, 1), D(11, 9)));

    [Fact]
    public void MultiDayEvent_ShowsOnEveryDayItCovers()
    {
        var e = Ev("night", D(11, 3, 22), D(11, 4, 2));
        Assert.Single(Recurrence.Between(e, D(11, 4), D(11, 4)));
        Assert.Single(Recurrence.Between(e, D(11, 3), D(11, 3)));
        Assert.Empty(Recurrence.Between(e, D(11, 5), D(11, 5)));
    }

    [Fact]
    public void AllDayMultiDay_CoversItsLastDayButNotTheNext()
    {
        var e = Ev("trip", D(11, 10), D(11, 12), allDay: true);
        Assert.Single(Recurrence.Between(e, D(11, 12), D(11, 12)));
        Assert.Empty(Recurrence.Between(e, D(11, 13), D(11, 13)));
    }

    [Fact]
    public void RepeatingOvernightEvent_ShowsTheOccurrenceThatStartedTheDayBefore()
    {
        var e = Ev("shift", D(11, 3, 22), D(11, 4, 2), repeat: Repeat.Daily);
        var onThe5th = Recurrence.Between(e, D(11, 5), D(11, 5)).Select(o => o.Start).ToList();
        Assert.Equal(new[] { D(11, 4, 22), D(11, 5, 22) }, onThe5th);
    }

    [Fact]
    public void EndBeforeStart_IsTreatedAsAZeroLengthEvent() =>
        Assert.Single(Recurrence.Between(Ev("odd", D(11, 3, 9), D(11, 3, 8)), D(11, 3), D(11, 3)));

    [Fact]
    public void FarAwayRange_DoesNotHangForMonthlyEvents() =>
        Assert.NotEmpty(Recurrence.Between(Ev("m", D(1, 1, 9), repeat: Repeat.Monthly), D(1, 1, 0, 0, 2100), D(12, 31, 0, 0, 2100)));

    // ---- the event store
    [Fact]
    public void Store_AddUpdateDelete_AndPersist()
    {
        var path = Path.Combine(_dir, "c.json");
        var store = new CalendarStore(path);
        var e = store.Add(Ev("Dentist", D(11, 3, 15)));
        store.Update(e.Id, x => x.Title = "Dentist (moved)");
        Assert.Equal("Dentist (moved)", new CalendarStore(path).All.Single().Title);
        store.Delete(e.Id);
        Assert.Empty(new CalendarStore(path).All);
    }

    [Fact]
    public void EventsOn_AllDayFirst_ThenByTime()
    {
        var store = new CalendarStore(Path.Combine(_dir, "o.json"));
        store.Add(Ev("late", D(11, 3, 18)));
        store.Add(Ev("holiday", D(11, 3), D(11, 3), allDay: true));
        store.Add(Ev("early", D(11, 3, 8)));
        Assert.Equal(new[] { "holiday", "early", "late" }, store.EventsOn(D(11, 3)).Select(o => o.Event.Title));
    }

    [Fact]
    public void DaysWithEvents_IncludesRepeatsAndMultiDay()
    {
        var store = new CalendarStore(Path.Combine(_dir, "d.json"));
        store.Add(Ev("weekly", D(11, 3, 9), repeat: Repeat.Weekly));
        store.Add(Ev("trip", D(11, 12), D(11, 14), allDay: true));
        var days = store.DaysWithEvents(2026, 11);
        Assert.Contains(3, days.Keys); Assert.Contains(10, days.Keys); Assert.Contains(24, days.Keys);
        Assert.Contains(13, days.Keys); Assert.DoesNotContain(4, days.Keys);
    }

    [Fact]
    public void Upcoming_ListsFutureOccurrencesInOrder_SkippingPastOnes()
    {
        var store = new CalendarStore(Path.Combine(_dir, "u.json"));
        store.Add(Ev("past", D(11, 3, 7)));
        store.Add(Ev("soon", D(11, 3, 12)));
        store.Add(Ev("daily", D(11, 3, 9), repeat: Repeat.Daily));
        var next = store.Upcoming(D(11, 3, 10), days: 2, count: 3).Select(o => (o.Event.Title, o.Start)).ToList();
        Assert.Equal(new[] { ("soon", D(11, 3, 12)), ("daily", D(11, 4, 9)), ("daily", D(11, 5, 9)) }, next);
    }

    [Fact]
    public void CorruptFile_StartsEmpty_AndKeepsACopy()
    {
        var path = Path.Combine(_dir, "bad.json");
        File.WriteAllText(path, "{oops");
        var store = new CalendarStore(path);
        Assert.Empty(store.All);
        store.Add(Ev("new", D(11, 3, 9)));
        Assert.Single(Directory.GetFiles(_dir, "bad.json.corrupt-*"));
    }

    // ---- reminders
    [Fact]
    public void Reminder_FiresOnceInsideItsWindow()
    {
        var e = Ev("call", D(11, 3, 10), reminder: 15);
        var fired = new HashSet<string>();
        var due = ReminderScheduler.Due(new[] { e }, now: D(11, 3, 9, 46), lastCheck: D(11, 3, 9, 44), fired, TimeSpan.FromMinutes(15));
        Assert.Equal(D(11, 3, 10), Assert.Single(due).Start);
        Assert.Empty(ReminderScheduler.Due(new[] { e }, D(11, 3, 9, 47), D(11, 3, 9, 46), fired, TimeSpan.FromMinutes(15))); // already shown
    }

    [Fact]
    public void Reminder_NotDueBeforeItsTime_OrWhenSwitchedOff()
    {
        var fired = new HashSet<string>();
        Assert.Empty(ReminderScheduler.Due(new[] { Ev("x", D(11, 3, 10), reminder: 15) }, D(11, 3, 9, 30), D(11, 3, 9, 29), fired, TimeSpan.FromMinutes(15)));
        Assert.Empty(ReminderScheduler.Due(new[] { Ev("y", D(11, 3, 10), reminder: -1) }, D(11, 3, 10, 5), D(11, 3, 9, 0), fired, TimeSpan.FromMinutes(15)));
    }

    [Fact]
    public void Reminder_AtStart_Works() =>
        Assert.Single(ReminderScheduler.Due(new[] { Ev("z", D(11, 3, 10), reminder: 0) }, D(11, 3, 10, 0), D(11, 3, 9, 59), new HashSet<string>(), TimeSpan.FromMinutes(15)));

    [Fact]
    public void Reminder_MissedByMoreThanMaxLate_IsSkipped_NotShownHoursLater()
    {
        // the PC slept from 07:00 to 12:00: a 09:45 reminder must not pop up at noon
        var e = Ev("call", D(11, 3, 10), reminder: 15);
        Assert.Empty(ReminderScheduler.Due(new[] { e }, D(11, 3, 12), D(11, 3, 7), new HashSet<string>(), TimeSpan.FromMinutes(15)));
    }

    [Fact]
    public void Reminder_ForRepeatingEvents_FiresForEachOccurrenceSeparately()
    {
        var e = Ev("standup", D(11, 3, 10), repeat: Repeat.Daily, reminder: 5);
        var fired = new HashSet<string>();
        Assert.Single(ReminderScheduler.Due(new[] { e }, D(11, 4, 9, 56), D(11, 4, 9, 54), fired, TimeSpan.FromMinutes(15)));
        Assert.Single(ReminderScheduler.Due(new[] { e }, D(11, 5, 9, 56), D(11, 5, 9, 54), fired, TimeSpan.FromMinutes(15)));
    }

    [Fact]
    public void Reminder_AllDayEvents_RemindAt0900()
    {
        var e = Ev("birthday", D(11, 4), D(11, 4), allDay: true, reminder: 0);
        Assert.Single(ReminderScheduler.Due(new[] { e }, D(11, 4, 9, 1), D(11, 4, 8, 59), new HashSet<string>(), TimeSpan.FromMinutes(15)));
    }

    [Fact]
    public void FiredKeys_AreStoredAndPrunedWhenOld()
    {
        var path = Path.Combine(_dir, "fired.json");
        var keys = new FiredReminders(path);
        keys.Add("a|1", D(11, 3, 10)); keys.Add("b|2", D(11, 8, 10));
        keys.Save(now: D(11, 8, 12));
        var again = new FiredReminders(path);
        Assert.False(again.Contains("a|1"));   // older than two days: dropped
        Assert.True(again.Contains("b|2"));
    }

    // ---- month grid and times
    [Fact]
    public void MonthGrid_Has42Days_StartingOnTheRightWeekday()
    {
        var sunday = MonthGrid.Build(2026, 11, DayOfWeek.Sunday);
        Assert.Equal(42, sunday.Count);
        Assert.Equal(D(11, 1), sunday[0]);
        var monday = MonthGrid.Build(2026, 11, DayOfWeek.Monday);
        Assert.Equal(D(10, 26), monday[0]);
        Assert.Equal(D(12, 6), monday[^1]);
        Assert.Contains(D(11, 30), monday);
    }

    [Theory]
    [InlineData("9:30", 9, 30)]
    [InlineData("09:30", 9, 30)]
    [InlineData("930", 9, 30)]
    [InlineData("1430", 14, 30)]
    [InlineData("9", 9, 0)]
    [InlineData("9pm", 21, 0)]
    [InlineData("9:30 PM", 21, 30)]
    [InlineData("12am", 0, 0)]
    [InlineData("12pm", 12, 0)]
    [InlineData(" 7:05 ", 7, 5)]
    public void TimeInput_Parses(string text, int hour, int minute) =>
        Assert.Equal(new TimeSpan(hour, minute, 0), TimeInput.TryParse(text));

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("25:00")]
    [InlineData("24:00")]
    [InlineData("9:75")]
    [InlineData("13pm")]
    public void TimeInput_RejectsNonsense(string text) => Assert.Null(TimeInput.TryParse(text));
}
