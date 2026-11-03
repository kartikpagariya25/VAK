using VAK.Core.Links;
using VAK.Core.Storage;

namespace VAK.Core.Clipboard;

public enum ClipKind { Text, Image, Link }

public class ClipEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public ClipKind Kind { get; set; }
    public string Text { get; set; } = "";
    public string ImageFile { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public bool Pinned { get; set; }
    public DateTime Time { get; set; }
}

public static class ClipboardText
{
    public static int WordCount(string text) =>
        text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
}

/// <summary>Everything copied recently: text, links and images. Pinned items stay on top and are never dropped by the size cap.</summary>
public class ClipboardHistory
{
    private readonly JsonListStore<ClipEntry> _store;
    private readonly Func<DateTime> _now;
    private readonly int _max;
    private readonly List<ClipEntry> _items;

    /// <summary>Raised with the image file name when an image entry is removed, so the app can delete the file.</summary>
    public event Action<string>? ImageDropped;

    public ClipboardHistory(string path, Func<DateTime> now, int max = 200)
    {
        _store = new JsonListStore<ClipEntry>(path);
        _now = now;
        _max = max;
        _items = _store.Load();
    }

    public IReadOnlyList<ClipEntry> All =>
        _items.OrderByDescending(e => e.Pinned).ThenByDescending(e => e.Time).ToList();

    /// <summary>Adds copied text. Blank text is ignored; the same text again just moves to the top.</summary>
    public ClipEntry? AddText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var existing = _items.FirstOrDefault(e => e.Kind != ClipKind.Image && e.Text == text);
        if (existing is not null)
        {
            existing.Time = _now();
            _store.Save(_items);
            return existing;
        }
        var isLink = !text.Trim().Contains('\n') && LinkNormalizer.Normalize(text.Trim()) is not null && text.Trim().Contains("://") ;
        return Add(new ClipEntry { Kind = isLink ? ClipKind.Link : ClipKind.Text, Text = text, Time = _now() });
    }

    public ClipEntry AddImage(string file, int width, int height) =>
        Add(new ClipEntry { Kind = ClipKind.Image, ImageFile = file, Width = width, Height = height, Time = _now() });

    private ClipEntry Add(ClipEntry entry)
    {
        _items.Add(entry);
        while (_items.Count > _max)
        {
            var oldest = _items.Where(e => !e.Pinned).OrderBy(e => e.Time).FirstOrDefault();
            if (oldest is null) break;
            Remove(oldest);
        }
        _store.Save(_items);
        return entry;
    }

    private void Remove(ClipEntry entry)
    {
        _items.Remove(entry);
        if (entry.Kind == ClipKind.Image) ImageDropped?.Invoke(entry.ImageFile);
    }

    public void SetPinned(string id, bool pinned)
    {
        var e = _items.FirstOrDefault(x => x.Id == id);
        if (e is null) return;
        e.Pinned = pinned;
        _store.Save(_items);
    }

    public void Delete(string id)
    {
        var e = _items.FirstOrDefault(x => x.Id == id);
        if (e is null) return;
        Remove(e);
        _store.Save(_items);
    }

    /// <summary>Removes everything except pinned items.</summary>
    public void Clear()
    {
        foreach (var e in _items.Where(x => !x.Pinned).ToList()) Remove(e);
        _store.Save(_items);
    }

    public int Count(ClipKind? kind) => kind is null ? _items.Count : _items.Count(e => e.Kind == kind);

    public IReadOnlyList<ClipEntry> Filter(ClipKind? kind, string query)
    {
        var q = query?.Trim() ?? "";
        return All.Where(e => (kind is null || e.Kind == kind) &&
                              (q.Length == 0 || (e.Kind != ClipKind.Image && e.Text.Contains(q, StringComparison.OrdinalIgnoreCase)))).ToList();
    }
}
