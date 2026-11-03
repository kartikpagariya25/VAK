using System.Text.Json;

namespace VAK.Core.Storage;

/// <summary>Persists a list of records as JSON.
/// - A corrupt file is moved aside (name.corrupt-time) so the next save cannot silently destroy it.
/// - If the file could not be read at all (locked, access denied) the store refuses to save for the rest of the session,
///   so data that was never loaded is never overwritten.
/// - A failed save is ignored: losing one save must never crash the app. Saves are flushed to disk before replacing the file.</summary>
public class JsonListStore<T>
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly string _path;
    private bool _loadFailed;

    public JsonListStore(string path) => _path = path;

    public List<T> Load()
    {
        try
        {
            if (!File.Exists(_path)) return new List<T>();
            return JsonSerializer.Deserialize<List<T>>(File.ReadAllText(_path), Options) ?? new List<T>();
        }
        catch (JsonException)
        {
            try { File.Move(_path, $"{_path}.corrupt-{DateTime.Now:yyyyMMddHHmmssfff}", true); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { _loadFailed = true; }
            return new List<T>();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _loadFailed = true; // the data is probably fine but unreadable right now
            return new List<T>();
        }
    }

    public void Save(IEnumerable<T> items)
    {
        if (_loadFailed) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temp = _path + ".tmp";
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, items, Options);
                stream.Flush(true);
            }
            File.Move(temp, _path, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}
