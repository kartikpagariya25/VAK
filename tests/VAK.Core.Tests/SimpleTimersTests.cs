using VAK.Core.Timers;
using Xunit;

namespace VAK.Core.Tests;

public class SimpleTimersTests
{
    [Fact]
    public void Countdown_ReachesZero_SetsFinished_ClampedAtZero()
    {
        var c = new CountdownTimer(TimeSpan.FromMinutes(15));
        c.Start();
        c.Tick(TimeSpan.FromMinutes(20));
        Assert.Equal(TimeSpan.Zero, c.Remaining);
        Assert.True(c.Finished);
        Assert.False(c.Running);
    }

    [Fact]
    public void Stopwatch_Format_1h2m3s450ms_Is_01_02_03_45()
    {
        var s = new StopwatchTimer();
        s.Start();
        s.Tick(new TimeSpan(0, 1, 2, 3, 450));
        Assert.Equal("01:02:03.45", s.Format());
    }

    [Fact]
    public void Water_Remove_AtZero_StaysZero()
    {
        var w = new WaterCounter();
        w.Remove();
        Assert.Equal(0, w.Count);
    }

    [Fact]
    public void Water_Default_Is_0_Of_8()
    {
        var w = new WaterCounter();
        Assert.Equal(0, w.Count);
        Assert.Equal(8, w.Goal);
    }
}
