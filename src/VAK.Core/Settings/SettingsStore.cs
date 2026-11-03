using System.Text.Json;

namespace VAK.Core.Settings;

public class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly string _path;

    public SettingsStore(string path) => _path = path;

    public AppSettings Load()
    {
        if (!File.Exists(_path)) return new AppSettings();
        try
        {
            var text = File.ReadAllText(_path);
            var loaded = JsonSerializer.Deserialize<AppSettings>(text, Options);
            if (loaded is null) throw new JsonException("empty settings");
            if (loaded.HomeSlots is not { Length: 6 }) loaded.HomeSlots = new string?[6];
            return loaded;
        }
        catch (Exception e) when (e is JsonException or IOException)
        {
            try { File.Copy(_path, _path + ".bak", true); } catch (IOException) { }
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, Options));
        File.Move(temp, _path, true);
    }
}
