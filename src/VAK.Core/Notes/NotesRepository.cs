using VAK.Core.Storage;

namespace VAK.Core.Notes;

public class NoteItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Body { get; set; } = "";
    public DateTime Created { get; set; }
    public DateTime Updated { get; set; }
}

public static class NoteText
{
    /// <summary>The first non-empty line, trimmed to 60 characters; "Untitled note" when there is none.</summary>
    public static string Title(string body)
    {
        var line = body.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0);
        if (line is null) return "Untitled note";
        return line.Length > 60 ? line[..60] : line;
    }
}

/// <summary>The saved notes, newest edit first. Every change is saved straight away.</summary>
public class NotesRepository
{
    private readonly JsonListStore<NoteItem> _store;
    private readonly Func<DateTime> _now;
    private readonly List<NoteItem> _items;

    public NotesRepository(string path, Func<DateTime> now)
    {
        _store = new JsonListStore<NoteItem>(path);
        _now = now;
        _items = _store.Load();
    }

    public IReadOnlyList<NoteItem> All => _items.OrderByDescending(n => n.Updated).ThenByDescending(n => n.Created).ToList();

    public NoteItem Create(string body = "")
    {
        var t = _now();
        var note = new NoteItem { Body = body, Created = t, Updated = t };
        _items.Add(note);
        _store.Save(_items);
        return note;
    }

    public void Update(string id, string body)
    {
        var note = _items.FirstOrDefault(n => n.Id == id);
        if (note is null || note.Body == body) return;
        note.Body = body;
        note.Updated = _now();
        _store.Save(_items);
    }

    public void Delete(string id)
    {
        if (_items.RemoveAll(n => n.Id == id) > 0) _store.Save(_items);
    }

    public IEnumerable<NoteItem> Search(string query) =>
        string.IsNullOrWhiteSpace(query) ? All : All.Where(n => n.Body.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase));
}
