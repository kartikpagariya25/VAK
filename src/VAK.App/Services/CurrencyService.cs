using System.IO;
using System.Net.Http;
using VAK.Core.Units;

namespace VAK.App.Services;

/// <summary>Loads currency rates (api.frankfurter.app, no key). Rates are cached on disk for 12 hours; offline falls back to the cache.</summary>
public sealed class CurrencyService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(6) };
    private readonly string _cachePath;
    private UnitCategory? _category;

    public CurrencyService(string dataDir) => _cachePath = Path.Combine(dataDir, "rates.json");

    public UnitCategory? Category => _category;

    /// <summary>Returns the Currency category, or null when there are no rates at all (offline and nothing cached).</summary>
    public async Task<UnitCategory?> LoadAsync()
    {
        if (_category is not null) return _category;
        var fresh = File.Exists(_cachePath) && DateTime.UtcNow - File.GetLastWriteTimeUtc(_cachePath) < TimeSpan.FromHours(12);
        string? json = fresh ? TryRead() : null;
        if (json is null)
        {
            try
            {
                json = await Http.GetStringAsync("https://api.frankfurter.app/latest?from=USD").ConfigureAwait(true);
                if (CurrencyRates.Parse(json) is not null) TryWrite(json);
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { json = TryRead(); }
        }
        var rates = json is null ? null : CurrencyRates.Parse(json);
        return _category = rates is null ? null : CurrencyRates.BuildCategory(rates);
    }

    private string? TryRead()
    {
        try { return File.Exists(_cachePath) ? File.ReadAllText(_cachePath) : null; }
        catch (IOException) { return null; }
    }

    private void TryWrite(string json)
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(_cachePath)!); File.WriteAllText(_cachePath, json); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}
