using VAK.Core.Shelf;
using Xunit;

namespace VAK.Core.Tests;

public class ShelfStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "vak-shelf-" + Guid.NewGuid().ToString("N"));
    private readonly string _src = Path.Combine(Path.GetTempPath(), "vak-shelfsrc-" + Guid.NewGuid().ToString("N"));

    public ShelfStoreTests() { Directory.CreateDirectory(_src); }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
        Directory.Delete(_src, true);
    }

    private string File1(string name, string content = "hello")
    {
        var p = Path.Combine(_src, name);
        File.WriteAllText(p, content);
        return p;
    }

    [Theory]
    [InlineData("a.txt", new string[0], "a.txt")]
    [InlineData("a.txt", new[] { "a.txt" }, "a (2).txt")]
    [InlineData("a.txt", new[] { "a.txt", "a (2).txt" }, "a (3).txt")]
    [InlineData("noext", new[] { "noext" }, "noext (2)")]
    [InlineData("A.TXT", new[] { "a.txt" }, "A (2).TXT")]
    public void UniqueName_AvoidsCollisions(string name, string[] existing, string expected) =>
        Assert.Equal(expected, ShelfStore.UniqueName(existing, name));

    [Theory]
    [InlineData("  My files  ", "My files")]
    [InlineData("a/b:c*d", "abcd")]
    [InlineData("", null)]
    [InlineData("...", null)]
    public void CleanShelfName_RemovesInvalidChars(string input, string? expected) =>
        Assert.Equal(expected, ShelfStore.CleanShelfName(input));

    [Fact]
    public void NewStore_HasDeskShelf() =>
        Assert.Equal(new[] { "Desk" }, new ShelfStore(_root).Shelves());

    [Fact]
    public void CreateShelf_AddsUniquely_AndDeleteWorks_ButNotDesk()
    {
        var s = new ShelfStore(_root);
        Assert.Equal("Work", s.CreateShelf("Work"));
        Assert.Equal("Work (2)", s.CreateShelf("Work"));
        Assert.Null(s.CreateShelf("///"));
        Assert.True(s.DeleteShelf("Work"));
        Assert.False(s.DeleteShelf("Desk"));
        Assert.Equal(new[] { "Desk", "Work (2)" }, s.Shelves());
    }

    [Fact]
    public void AddFile_CopiesIt_AndKeepsTheOriginal()
    {
        var s = new ShelfStore(_root);
        var item = s.AddFile("Desk", File1("doc.txt", "12345"))!;
        Assert.Equal("doc.txt", item.Name);
        Assert.Equal(5, item.Size);
        Assert.True(File.Exists(item.Path));
        Assert.True(File.Exists(Path.Combine(_src, "doc.txt")));
    }

    [Fact]
    public void AddFile_SameNameTwice_GetsNumbered()
    {
        var s = new ShelfStore(_root);
        s.AddFile("Desk", File1("doc.txt"));
        var second = s.AddFile("Desk", Path.Combine(_src, "doc.txt"))!;
        Assert.Equal("doc (2).txt", second.Name);
    }

    [Fact]
    public void AddFile_MissingOrDirectory_ReturnsNull()
    {
        var s = new ShelfStore(_root);
        Assert.Null(s.AddFile("Desk", Path.Combine(_src, "nope.txt")));
        Assert.Null(s.AddFile("Desk", _src));
        Assert.Null(s.AddFile("NoSuchShelf", File1("x.txt")));
    }

    [Fact]
    public void Items_SortByNameSizeAndRecent()
    {
        var s = new ShelfStore(_root);
        var big = s.AddFile("Desk", File1("b.txt", "1234567890"))!;
        var small = s.AddFile("Desk", File1("c.txt", "1"))!;
        var first = s.AddFile("Desk", File1("a.txt", "12345"))!;
        File.SetLastWriteTimeUtc(big.Path, new DateTime(2026, 1, 1));
        File.SetLastWriteTimeUtc(small.Path, new DateTime(2026, 1, 2));
        File.SetLastWriteTimeUtc(first.Path, new DateTime(2026, 1, 3));
        Assert.Equal(new[] { "a.txt", "c.txt", "b.txt" }, s.Items("Desk", ShelfSort.Recent).Select(i => i.Name));
        Assert.Equal(new[] { "a.txt", "b.txt", "c.txt" }, s.Items("Desk", ShelfSort.Name).Select(i => i.Name));
        Assert.Equal(new[] { "b.txt", "a.txt", "c.txt" }, s.Items("Desk", ShelfSort.Size).Select(i => i.Name));
    }

    [Fact]
    public void RemoveAndClear_DeleteFiles()
    {
        var s = new ShelfStore(_root);
        s.AddFile("Desk", File1("a.txt")); s.AddFile("Desk", File1("b.txt"));
        s.Remove("Desk", "a.txt");
        Assert.Equal(new[] { "b.txt" }, s.Items("Desk", ShelfSort.Name).Select(i => i.Name));
        s.Clear("Desk");
        Assert.Empty(s.Items("Desk", ShelfSort.Name));
    }

    [Fact]
    public void Remove_RefusesPathTricks()
    {
        var s = new ShelfStore(_root);
        var outside = File1("keep.txt");
        s.Remove("Desk", @"..\..\" + Path.GetFileName(_src) + @"\keep.txt");
        s.Remove("..", "keep.txt");
        Assert.True(File.Exists(outside));
    }

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(999, "999 B")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(5 * 1024 * 1024, "5 MB")]
    [InlineData(3L * 1024 * 1024 * 1024, "3 GB")]
    public void FormatSize_PicksUnit(long bytes, string expected) => Assert.Equal(expected, ShelfStore.FormatSize(bytes));
}
