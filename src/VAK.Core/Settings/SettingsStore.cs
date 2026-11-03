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
            return Normalize(loaded);
        }
        catch (Exception e) when (e is JsonException or IOException)
        {
            try { File.Copy(_path, _path + ".bak", true); } catch (IOException) { }
            return new AppSettings();
        }
    }

    /// <summary>An explicit null in the file (for example {"Dock":null}) deserializes without error; replace each with its default.</summary>
    private static AppSettings Normalize(AppSettings loaded)
    {
        var defaults = new AppSettings();
        if (loaded.HomeSlots is not { Length: 6 }) loaded.HomeSlots = defaults.HomeSlots;
        if (loaded.Dock is null) loaded.Dock = defaults.Dock;
        if (loaded.Clock is null) loaded.Clock = defaults.Clock;
        if (loaded.Timers is null) loaded.Timers = defaults.Timers;
        if (loaded.Weather is null) loaded.Weather = defaults.Weather;
        if (loaded.Message is null) loaded.Message = defaults.Message;
        if (loaded.Converter is null) loaded.Converter = defaults.Converter;
        if (loaded.WorldClocks is null) loaded.WorldClocks = defaults.WorldClocks;
        return loaded;
    }

    /// <summary>Saves atomically. A locked file or a denied folder is swallowed: losing one save must never crash the app.</summary>
    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temp = _path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, Options));
            File.Move(temp, _path, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}
