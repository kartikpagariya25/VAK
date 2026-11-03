using VAK.Core.Clipboard;
using Xunit;

namespace VAK.Core.Tests;

public class ClipboardHistoryTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vak-cb-" + Guid.NewGuid().ToString("N"));
    private DateTime _now = new(2026, 11, 3, 12, 0, 0);
    public ClipboardHistoryTests() => Directory.CreateDirectory(_dir);
    public void Dispose() => Directory.Delete(_dir, true);

    private ClipboardHistory New(int max = 200) => new(Path.Combine(_dir, "h.json"), () => _now, max);

    [Theory]
    [InlineData("hello world", ClipKind.Text)]
    [InlineData("https://example.com/a?b=1", ClipKind.Link)]
    [InlineData("  http://localhost:3000  ", ClipKind.Link)]
    [InlineData("see https://example.com now", ClipKind.Text)]
    [InlineData("javascript:alert(1)", ClipKind.Text)]
    public void AddText_Classifies(string text, ClipKind expected) =>
        Assert.Equal(expected, New().AddText(text)!.Kind);

    [Theory]
    [InlineData("")]
    [InlineData("   \n ")]
    public void AddText_IgnoresBlank(string text) => Assert.Null(New().AddText(text));

    [Fact]
    public void AddText_SameTextAgain_MovesToTopInsteadOfDuplicating()
    {
        var h = New();
        h.AddText("a"); _now = _now.AddSeconds(1);
        h.AddText("b"); _now = _now.AddSeconds(1);
        h.AddText("a");
        Assert.Equal(new[] { "a", "b" }, h.All.Select(e => e.Text));
    }

    [Fact]
    public void All_IsNewestFirst_AndPinnedStayOnTop()
    {
        var h = New();
        var a = h.AddText("a")!; _now = _now.AddSeconds(1);
        h.AddText("b"); _now = _now.AddSeconds(1);
        h.AddText("c");
        h.SetPinned(a.Id, true);
        Assert.Equal(new[] { "a", "c", "b" }, h.All.Select(e => e.Text));
    }

    [Fact]
    public void Cap_DropsOldestUnpinned_NeverPinned()
    {
        var h = New(max: 3);
        var first = h.AddText("one")!; h.SetPinned(first.Id, true); _now = _now.AddSeconds(1);
        h.AddText("two"); _now = _now.AddSeconds(1);
        h.AddText("three"); _now = _now.AddSeconds(1);
        h.AddText("four");
        Assert.Equal(3, h.All.Count);
        Assert.Contains(h.All, e => e.Text == "one");
        Assert.DoesNotContain(h.All, e => e.Text == "two");
    }

    [Fact]
    public void Filter_AndCounts_ByKind()
    {
        var h = New();
        h.AddText("plain"); h.AddText("https://a.com"); h.AddImage("x.png", 10, 20);
        Assert.Equal((3, 1, 1, 1), (h.Count(null), h.Count(ClipKind.Text), h.Count(ClipKind.Image), h.Count(ClipKind.Link)));
        Assert.Single(h.Filter(ClipKind.Image, ""));
        Assert.Equal("plain", Assert.Single(h.Filter(null, "PLA")).Text);
    }

    [Fact]
    public void AddImage_StoresSize()
    {
        var e = New().AddImage("pic.png", 951, 456);
        Assert.Equal((ClipKind.Image, 951, 456, "pic.png"), (e.Kind, e.Width, e.Height, e.ImageFile));
    }

    [Fact]
    public void Delete_RemovesEntry_AndClearKeepsPinned()
    {
        var h = New();
        var a = h.AddText("a")!; h.AddText("b");
        h.Delete(a.Id);
        Assert.Single(h.All);
        h.SetPinned(h.All[0].Id, true); h.AddText("c");
        h.Clear();
        Assert.Equal("b", Assert.Single(h.All).Text);
    }

    [Fact]
    public void Persists_AcrossInstances()
    {
        New().AddText("kept");
        Assert.Equal("kept", Assert.Single(New().All).Text);
    }

    [Theory]
    [InlineData("one two  three", 3)]
    [InlineData("", 0)]
    [InlineData("single", 1)]
    public void WordCount(string text, int words) => Assert.Equal(words, ClipboardText.WordCount(text));
}
