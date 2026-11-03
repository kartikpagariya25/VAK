using VAK.Core.ScreenTime;
using Xunit;

namespace VAK.Core.Tests;

public class ScreenTimeTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vak-st-" + Guid.NewGuid().ToString("N"));
    private DateTime _now = new(2026, 11, 3, 12, 0, 0);
    public ScreenTimeTests() => Directory.CreateDirectory(_dir);
    public void Dispose() => Directory.Delete(_dir, true);

    private ScreenTimeLog New() => new(Path.Combine(_dir, "st.json"), () => _now);

    [Fact]
    public void Add_AccumulatesPerAppPerDay()
    {
        var log = New();
        log.Add("chrome", "Google Chrome", 30);
        log.Add("chrome", "Google Chrome", 45);
        log.Add("code", "VS Code", 10);
        Assert.Equal(85, log.TotalSeconds(_now));
        Assert.Equal(2, log.AppsUsed(_now));
    }

    [Fact]
    public void TopApps_SortedWithPercentages()
    {
        var log = New();
        log.Add("a", "A", 60); log.Add("b", "B", 180); log.Add("c", "C", 60);
        var top = log.TopApps(_now, 2);
        Assert.Equal(new[] { "B", "A" }, top.Select(t => t.Name)); // ties keep a stable order
        Assert.Equal(60, top[0].Percent); // 180 of 300
    }

    [Fact]
    public void Last7Days_OldestFirst_ZeroFilled()
    {
        var log = New();
        log.Add("a", "A", 100);
        _now = _now.AddDays(-2); log.Add("a", "A", 50);
        _now = _now.AddDays(2);
        var days = log.Last7Days();
        Assert.Equal(7, days.Count);
        Assert.Equal(new DateTime(2026, 10, 28), days[0].Date);
        Assert.Equal(new DateTime(2026, 11, 3), days[6].Date);
        Assert.Equal(new double[] { 0, 0, 0, 0, 50, 0, 100 }, days.Select(d => d.Seconds));
    }

    [Fact]
    public void AverageOfActiveDays_IgnoresEmptyDays()
    {
        var log = New();
        log.Add("a", "A", 100);
        _now = _now.AddDays(-1); log.Add("a", "A", 50);
        _now = _now.AddDays(1);
        Assert.Equal(75, log.AverageSeconds());
    }

    [Theory]
    [InlineData(420, 360, 24 * 60 - 24 * 60 + 60)] // 7h limit, 6h used -> 1h left
    [InlineData(60, 4000, 0)]                      // over the limit never goes negative
    public void LimitRemaining_ClampsAtZero(int limitMinutes, double usedSeconds, int expectedMinutes)
    {
        var log = New();
        log.Add("a", "A", limitMinutes == 420 ? 360 * 60 : usedSeconds);
        Assert.Equal(expectedMinutes * 60, log.RemainingSeconds(_now, limitMinutes));
    }

    [Fact]
    public void Save_Persists_AndPrunesOldRows()
    {
        var log = New();
        log.Add("a", "A", 10);
        _now = _now.AddDays(-90); log.Add("old", "Old", 10);
        _now = _now.AddDays(90);
        log.Save();
        var reloaded = New();
        Assert.Equal(1, reloaded.AppsUsed(_now));
        Assert.Equal(0, reloaded.TotalSeconds(_now.AddDays(-90)));
    }

    [Theory]
    [InlineData(0, "0m")]
    [InlineData(20, "<1m")]
    [InlineData(60, "1m")]
    [InlineData(390, "6m")]
    [InlineData(3600, "1h")]
    [InlineData(24780, "6h 53m")]
    public void DurationFormatter_Short(double seconds, string expected) =>
        Assert.Equal(expected, DurationFormatter.Short(seconds));
}
