using VAK.Core.Links;
using VAK.Core.Notes;
using VAK.Core.Storage;
using Xunit;

namespace VAK.Core.Tests;

public class NotesAndLinksTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vak-nl-" + Guid.NewGuid().ToString("N"));
    private string P(string name) => Path.Combine(_dir, name);
    public NotesAndLinksTests() => Directory.CreateDirectory(_dir);
    public void Dispose() => Directory.Delete(_dir, true);

    // ---- JsonListStore
    public record Item(string Name, int Count);

    [Fact]
    public void ListStore_RoundTrips()
    {
        var s = new JsonListStore<Item>(P("a.json"));
        s.Save(new[] { new Item("x", 1), new Item("y", 2) });
        Assert.Equal(new[] { "x", "y" }, new JsonListStore<Item>(P("a.json")).Load().Select(i => i.Name));
    }

    [Theory]
    [InlineData("")]
    [InlineData("{not json")]
    [InlineData("{\"a\":1}")]
    public void ListStore_BadFile_ReturnsEmpty(string content)
    {
        File.WriteAllText(P("b.json"), content);
        Assert.Empty(new JsonListStore<Item>(P("b.json")).Load());
    }

    [Fact]
    public void ListStore_MissingFile_ReturnsEmpty() => Assert.Empty(new JsonListStore<Item>(P("none.json")).Load());

    [Fact]
    public void ListStore_UnwritablePath_DoesNotThrow()
    {
        File.WriteAllText(P("blocker"), "x");
        new JsonListStore<Item>(Path.Combine(P("blocker"), "x.json")).Save(new[] { new Item("a", 1) });
    }

    // ---- Notes
    [Theory]
    [InlineData("Shopping list\nmilk", "Shopping list")]
    [InlineData("\n\n  Hello  \nworld", "Hello")]
    [InlineData("", "Untitled note")]
    [InlineData("   \n  ", "Untitled note")]
    public void NoteTitle_IsFirstNonEmptyLine(string body, string expected) =>
        Assert.Equal(expected, NoteText.Title(body));

    [Fact]
    public void NoteTitle_IsTrimmedTo60Chars() =>
        Assert.Equal(60, NoteText.Title(new string('a', 200)).Length);

    [Fact]
    public void Notes_CreateUpdateDelete_AndOrderByUpdatedDesc()
    {
        var t = new DateTime(2026, 11, 3, 10, 0, 0);
        var repo = new NotesRepository(P("n.json"), () => t);
        var a = repo.Create("first");
        t = t.AddMinutes(1);
        var b = repo.Create("second");
        Assert.Equal(new[] { b.Id, a.Id }, repo.All.Select(n => n.Id));

        t = t.AddMinutes(1);
        repo.Update(a.Id, "first edited");
        Assert.Equal(a.Id, repo.All[0].Id);
        Assert.Equal("first edited", repo.All[0].Body);

        repo.Delete(b.Id);
        Assert.Single(repo.All);
    }

    [Fact]
    public void Notes_Search_MatchesBodyCaseInsensitively()
    {
        var repo = new NotesRepository(P("s.json"), () => DateTime.Now);
        repo.Create("Buy MILK today");
        repo.Create("call mom");
        Assert.Equal("Buy MILK today", Assert.Single(repo.Search("milk")).Body);
        Assert.Equal(2, repo.Search("").Count());
    }

    [Fact]
    public void Notes_Persist_AcrossInstances()
    {
        new NotesRepository(P("p.json"), () => DateTime.Now).Create("kept");
        Assert.Equal("kept", Assert.Single(new NotesRepository(P("p.json"), () => DateTime.Now).All).Body);
    }

    [Fact]
    public void Notes_UpdateUnknownId_IsNoOp()
    {
        var repo = new NotesRepository(P("u.json"), () => DateTime.Now);
        repo.Update("nope", "x");
        Assert.Empty(repo.All);
    }

    // ---- Links
    [Theory]
    [InlineData("example.com", "https://example.com/")]
    [InlineData("  https://example.com/a?b=1 ", "https://example.com/a?b=1")]
    [InlineData("http://localhost:3000", "http://localhost:3000/")]
    public void Normalize_AcceptsWebAddresses(string input, string expected) =>
        Assert.Equal(expected, LinkNormalizer.Normalize(input));

    [Theory]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/Windows/win.ini")]
    [InlineData("ftp://example.com")]
    public void Normalize_RejectsEverythingElse(string input) => Assert.Null(LinkNormalizer.Normalize(input));

    [Fact]
    public void SearchEngines_BuildEscapedUrls()
    {
        Assert.Equal("https://www.google.com/search?q=hello%20world%26more", SearchEngines.BuildUrl("google", "hello world&more"));
        Assert.Equal("https://github.com/search?q=wpf", SearchEngines.BuildUrl("github", "wpf"));
        Assert.Null(SearchEngines.BuildUrl("nope", "x"));
        Assert.Equal(6, SearchEngines.All.Count);
    }

    [Fact]
    public void Links_AddRejectsInvalid_AndDedupes()
    {
        var repo = new LinksRepository(P("l.json"));
        Assert.Null(repo.Add("javascript:alert(1)", null));
        var a = repo.Add("example.com", null)!;
        Assert.Equal("example.com", a.Title); // title defaults to the host
        Assert.Null(repo.Add("https://example.com", "again")); // same address is not added twice
        repo.Remove(a.Id);
        Assert.Empty(repo.All);
    }
}
