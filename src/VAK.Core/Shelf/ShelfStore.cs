namespace VAK.Core.Shelf;

public enum ShelfSort { Recent, Name, Size }

public record ShelfItem(string Name, string Path, long Size, DateTime Added);

/// <summary>Shelves are folders under one root; a shelf's files are copies, so the originals are never touched.
/// Shelf and file names are always checked so nothing outside the root can be read or deleted.</summary>
public class ShelfStore
{
    public const string DefaultShelf = "Desk";
    private const long MaxFileBytes = 2L * 1024 * 1024 * 1024;

    private readonly string _root;

    public ShelfStore(string root)
    {
        _root = root;
        TryCreate(Path.Combine(_root, DefaultShelf));
    }

    /// <summary>"999 B", "1.5 KB", "5 MB".</summary>
    public static string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double v = bytes;
        var u = 0;
        while (v >= 1024 && u < units.Length - 1) { v /= 1024; u++; }
        return Math.Round(v, 1).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " " + units[u];
    }

    public IReadOnlyList<string> Shelves()
    {
        TryCreate(Path.Combine(_root, DefaultShelf));
        try
        {
            return Directory.GetDirectories(_root).Select(Path.GetFileName).OfType<string>()
                .OrderBy(n => n == DefaultShelf ? 0 : 1).ThenBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return new[] { DefaultShelf }; }
    }

    /// <summary>Removes characters that cannot be in a folder name; null when nothing usable is left.</summary>
    public static string? CleanShelfName(string name)
    {
        var cleaned = new string((name ?? "").Where(c => !Path.GetInvalidFileNameChars().Contains(c)).ToArray()).Trim().Trim('.').Trim();
        if (cleaned.Length > 30) cleaned = cleaned[..30].Trim();
        return cleaned.Length == 0 ? null : cleaned;
    }

    /// <summary>"a.txt" becomes "a (2).txt" when a.txt exists (names compare without regard to case).</summary>
    public static string UniqueName(IEnumerable<string> existing, string name)
    {
        var taken = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
        if (!taken.Contains(name)) return name;
        var ext = Path.GetExtension(name);
        var stem = Path.GetFileNameWithoutExtension(name);
        if (stem.Length == 0) { stem = name; ext = ""; } // a dot-file such as ".gitignore"
        for (var n = 2; ; n++)
        {
            var candidate = $"{stem} ({n}){ext}";
            if (!taken.Contains(candidate)) return candidate;
        }
    }

    public string? CreateShelf(string name)
    {
        var clean = CleanShelfName(name);
        if (clean is null) return null;
        var unique = UniqueName(Shelves(), clean);
        return TryCreate(Path.Combine(_root, unique)) ? unique : null;
    }

    public bool DeleteShelf(string shelf)
    {
        if (shelf == DefaultShelf || !ShelfDir(shelf, out var dir)) return false;
        try { Directory.Delete(dir, true); return true; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }

    public IReadOnlyList<ShelfItem> Items(string shelf, ShelfSort sort)
    {
        if (!ShelfDir(shelf, out var dir)) return Array.Empty<ShelfItem>();
        try
        {
            var items = new DirectoryInfo(dir).GetFiles()
                .Select(f => new ShelfItem(f.Name, f.FullName, f.Length, f.LastWriteTimeUtc));
            return (sort switch
            {
                ShelfSort.Name => items.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase),
                ShelfSort.Size => items.OrderByDescending(i => i.Size).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase),
                _ => items.OrderByDescending(i => i.Added).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase),
            }).ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return Array.Empty<ShelfItem>(); }
    }

    /// <summary>Copies a file onto a shelf. Returns null for a missing file, a folder, a file over 2 GB or an unknown shelf.</summary>
    public ShelfItem? AddFile(string shelf, string sourcePath)
    {
        if (!ShelfDir(shelf, out var dir) || !File.Exists(sourcePath)) return null;
        try
        {
            var info = new FileInfo(sourcePath);
            if (info.Length > MaxFileBytes) return null;
            var name = UniqueName(Directory.GetFiles(dir).Select(Path.GetFileName).OfType<string>(), info.Name);
            var target = Path.Combine(dir, name);
            File.Copy(sourcePath, target, false);
            File.SetLastWriteTimeUtc(target, DateTime.UtcNow); // "recent" means when it was added, not when the file was made
            return new ShelfItem(name, target, info.Length, DateTime.UtcNow);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }

    public void Remove(string shelf, string fileName)
    {
        if (!ShelfDir(shelf, out var dir) || Path.GetFileName(fileName) != fileName || fileName is "" or "." or "..") return;
        try { File.Delete(Path.Combine(dir, fileName)); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    public void Clear(string shelf)
    {
        if (!ShelfDir(shelf, out var dir)) return;
        foreach (var f in Items(shelf, ShelfSort.Name)) Remove(shelf, f.Name);
    }

    /// <summary>Resolves a shelf name to its folder, only if it is a plain name of an existing shelf.</summary>
    private bool ShelfDir(string shelf, out string dir)
    {
        dir = "";
        if (string.IsNullOrEmpty(shelf) || Path.GetFileName(shelf) != shelf || shelf is "." or "..") return false;
        dir = Path.Combine(_root, shelf);
        return Directory.Exists(dir);
    }

    private static bool TryCreate(string dir)
    {
        try { Directory.CreateDirectory(dir); return true; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }
}
