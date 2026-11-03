using VAK.Core.Settings;
using VAK.Core.Timers;
using Xunit;

namespace VAK.Core.Tests;

public class PomodoroEngineTests
{
    private static PomodoroEngine New(bool auto = true) =>
        new(new TimerSettings { AutoStartNext = auto });

    private static void Finish(PomodoroEngine e) => e.Tick(e.Remaining);

    [Fact]
    public void Initial_IsFocus_25Minutes_CycleOf1()
    {
        var e = New();
        Assert.Equal(PomodoroPhase.Focus, e.Phase);
        Assert.Equal(TimeSpan.FromMinutes(25), e.Remaining);
        Assert.Equal(1, e.CycleIndex);
        Assert.False(e.Running);
    }

    [Fact]
    public void Tick_WhileStopped_DoesNothing()
    {
        var e = New();
        e.Tick(TimeSpan.FromMinutes(1));
        Assert.Equal(TimeSpan.FromMinutes(25), e.Remaining);
    }

    [Fact]
    public void FocusCompletes_GoesToBreak_5Minutes_IncrementsCompleted()
    {
        var e = New();
        e.Start();
        Finish(e);
        Assert.Equal(PomodoroPhase.Break, e.Phase);
        Assert.Equal(TimeSpan.FromMinutes(5), e.Remaining);
        Assert.Equal(1, e.CompletedToday);
    }

    [Fact]
    public void FourthFocus_GoesToLongBreak_15Minutes()
    {
        var e = New();
        e.Start();
        for (var i = 0; i < 3; i++) { Finish(e); Finish(e); } // focus + break
        Finish(e); // 4th focus
        Assert.Equal(PomodoroPhase.LongBreak, e.Phase);
        Assert.Equal(TimeSpan.FromMinutes(15), e.Remaining);
    }

    [Fact]
    public void LongBreakEnd_ResetsToCycle1Focus()
    {
        var e = New();
        e.Start();
        for (var i = 0; i < 3; i++) { Finish(e); Finish(e); }
        Finish(e); // -> long break
        Finish(e); // long break ends
        Assert.Equal(PomodoroPhase.Focus, e.Phase);
        Assert.Equal(1, e.CycleIndex);
    }

    [Fact]
    public void AutoStartNextFalse_StopsAtPhaseEnd()
    {
        var e = New(auto: false);
        e.Start();
        Finish(e);
        Assert.False(e.Running);
        Assert.Equal(PomodoroPhase.Break, e.Phase);
    }

    [Fact]
    public void Pause_Resume_KeepsRemaining()
    {
        var e = New();
        e.Start();
        e.Tick(TimeSpan.FromMinutes(10));
        e.Pause();
        e.Tick(TimeSpan.FromMinutes(5));
        Assert.Equal(TimeSpan.FromMinutes(15), e.Remaining);
        e.Start();
        Assert.True(e.Running);
        Assert.Equal(TimeSpan.FromMinutes(15), e.Remaining);
    }

    [Fact]
    public void Skip_AdvancesPhase()
    {
        var e = New();
        e.Skip();
        Assert.Equal(PomodoroPhase.Break, e.Phase);
    }

    [Fact]
    public void HugeTick_AdvancesAtMostOnePhase()
    {
        var e = New();
        e.Start();
        e.Tick(TimeSpan.FromHours(3));
        Assert.Equal(PomodoroPhase.Break, e.Phase);
        Assert.Equal(TimeSpan.FromMinutes(5), e.Remaining);
        Assert.Equal(1, e.CompletedToday);
    }
}
