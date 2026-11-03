using VAK.Core.Widgets;
using Xunit;

namespace VAK.Core.Tests;

public class WidgetRegistryTests
{
    private static WidgetRegistry Sample()
    {
        var r = new WidgetRegistry();
        r.Register(new WidgetInfo("clock", "Clock", "Local Time & Date", "clock", new[] { "time", "date", "world" }, true));
        r.Register(new WidgetInfo("timers", "Timers", "Focus & intervals", "timers", new[] { "pomodoro", "stopwatch", "countdown" }, true));
        return r;
    }

    [Fact]
    public void Register_DuplicateId_Throws()
    {
        var r = Sample();
        Assert.Throws<InvalidOperationException>(() =>
            r.Register(new WidgetInfo("clock", "Clock 2", "", "clock", Array.Empty<string>(), true)));
    }

    [Fact]
    public void Search_MatchesTitleAndKeywordsCaseInsensitive()
    {
        var r = Sample();
        Assert.Equal("clock", Assert.Single(r.Search("CLOCK")).Id);
        Assert.Equal("timers", Assert.Single(r.Search("pomodoro")).Id);
        Assert.Equal(2, r.Search("").Count());
    }

    [Fact]
    public void Find_UnknownId_ReturnsNull()
    {
        Assert.Null(Sample().Find("nope"));
    }
}
