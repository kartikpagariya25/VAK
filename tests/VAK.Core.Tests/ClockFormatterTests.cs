using VAK.Core.Clock;
using Xunit;

namespace VAK.Core.Tests;

public class ClockFormatterTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 26, 0, TimeSpan.Zero);
    private static readonly TimeZoneInfo Ist = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");

    [Fact]
    public void Time_12Hour_NoSeconds_Is_5_56_PM() =>
        Assert.Equal("5:56 PM", ClockFormatter.Time(Now, Ist, false, false));

    [Fact]
    public void Time_24Hour_WithSeconds() =>
        Assert.Equal("17:56:00", ClockFormatter.Time(Now, Ist, true, true));

    [Fact]
    public void Date_Full() =>
        Assert.Equal("Tuesday, November 3, 2026", ClockFormatter.Date(TimeZoneInfo.ConvertTime(Now, Ist), "full"));

    [Fact]
    public void Date_Short() =>
        Assert.Equal("Tue, Nov 3", ClockFormatter.Date(TimeZoneInfo.ConvertTime(Now, Ist), "short"));

    [Theory]
    [InlineData("Eastern Standard Time", "-10h 30m")]
    [InlineData("GMT Standard Time", "-5h 30m")]
    [InlineData("Tokyo Standard Time", "+3h 30m")]
    [InlineData("India Standard Time", "")]
    public void OffsetLabel_RelativeToLocal(string zoneId, string expected) =>
        Assert.Equal(expected, ClockFormatter.OffsetLabel(Now, TimeZoneInfo.FindSystemTimeZoneById(zoneId), Ist));

    [Fact]
    public void HeaderDate_Is_Tuesday_Nov_3() =>
        Assert.Equal("Tuesday, Nov 3", ClockFormatter.HeaderDate(TimeZoneInfo.ConvertTime(Now, Ist)));

    [Fact]
    public void TryFind_UnknownId_ReturnsNull() =>
        Assert.Null(ClockFormatter.TryFind("Not A Real Zone"));
}
