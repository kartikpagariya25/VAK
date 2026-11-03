using VAK.Core.Timers;
using Xunit;

namespace VAK.Core.Tests;

public class TimerFormatterTests
{
    [Theory]
    [InlineData(25, 0, "25:00")]
    [InlineData(0, 0, "00:00")]
    [InlineData(65, 0, "65:00")]
    public void Mmss_WholeSeconds(int minutes, int seconds, string expected) =>
        Assert.Equal(expected, TimerFormatter.Mmss(new TimeSpan(0, minutes, seconds)));

    [Fact]
    public void Mmss_RoundsPartialSecondsUp() =>
        Assert.Equal("05:00", TimerFormatter.Mmss(new TimeSpan(0, 0, 4, 59, 200)));

    [Fact]
    public void Mmss_Negative_IsZero() =>
        Assert.Equal("00:00", TimerFormatter.Mmss(TimeSpan.FromSeconds(-3)));
}
