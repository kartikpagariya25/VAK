using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VAK.Core.Chat;

/// <summary>API keys, encrypted with Windows DPAPI so only this Windows user can read them. Keys never appear in settings.json.</summary>
public sealed class ApiKeyVault
{
    private readonly string _path;
    private readonly Dictionary<string, string> _keys;

    public ApiKeyVault(string path)
    {
        _path = path;
        _keys = Load();
    }

    private Dictionary<string, string> Load()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_path)) ?? new();
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException) { }
        return new();
    }

    public bool Has(string provider) => Get(provider) is not null;

    public string? Get(string provider)
    {
        if (!_keys.TryGetValue(provider, out var protectedText)) return null;
        try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(protectedText), null, DataProtectionScope.CurrentUser)); }
        catch (Exception e) when (e is CryptographicException or FormatException) { return null; }
    }

    public void Set(string provider, string key)
    {
        _keys[provider] = Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(key.Trim()), null, DataProtectionScope.CurrentUser));
        Save();
    }

    public void Remove(string provider)
    {
        if (_keys.Remove(provider)) Save();
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temp = _path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(_keys));
            File.Move(temp, _path, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}
