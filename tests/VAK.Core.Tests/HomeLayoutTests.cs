using VAK.Core.Widgets;
using Xunit;

namespace VAK.Core.Tests;

public class HomeLayoutTests
{
    [Fact]
    public void Default_Matches_TheReferenceHomeScreen() =>
        Assert.Equal(new string?[] { "screentime", "system", "weather", "timers", "clipboard", "notes" }, HomeLayout.Default().Slots);

    [Fact]
    public void Assign_SetsSlot()
    {
        var l = new HomeLayout(new string?[6]);
        l.Assign(4, "clock");
        Assert.Equal("clock", l.Slots[4]);
    }

    [Fact]
    public void Assign_ExistingWidget_MovesIt()
    {
        var l = HomeLayout.Default();
        l.Assign(5, "system");
        Assert.Null(l.Slots[1]);
        Assert.Equal("system", l.Slots[5]);
    }

    [Fact]
    public void Remove_ClearsSlot()
    {
        var l = HomeLayout.Default();
        l.Assign(1, null);
        Assert.Null(l.Slots[1]);
    }

    [Fact]
    public void Undo_RestoresPreviousLayout_ThenNoOp()
    {
        var l = HomeLayout.Default();
        l.Assign(3, "weather"); // moves weather from slot 2
        Assert.True(l.CanUndo);
        l.Undo();
        Assert.Equal(new string?[] { "screentime", "system", "weather", "timers", "clipboard", "notes" }, l.Slots);
        Assert.False(l.CanUndo);
        l.Undo(); // nothing left, must not throw
    }

    [Fact]
    public void Assign_OutOfRange_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new HomeLayout(new string?[6]).Assign(6, "clock"));

    [Fact]
    public void Constructor_WrongLength_PadsOrTrimsToSix()
    {
        Assert.Equal(6, new HomeLayout(new string?[2]).Slots.Length);
        Assert.Equal(6, new HomeLayout(new string?[9]).Slots.Length);
    }
}
