using System.Text.Json;

namespace VAK.Core.Storage;

/// <summary>Persists a list of records as JSON. A missing or corrupt file loads as an empty list; a failed save is ignored
/// (losing one save must never crash the app).</summary>
public class JsonListStore<T>
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly string _path;

    public JsonListStore(string path) => _path = path;

    public List<T> Load()
    {
        try
        {
            if (!File.Exists(_path)) return new List<T>();
            return JsonSerializer.Deserialize<List<T>>(File.ReadAllText(_path), Options) ?? new List<T>();
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            return new List<T>();
        }
    }

    public void Save(IEnumerable<T> items)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temp = _path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(items, Options));
            File.Move(temp, _path, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}
