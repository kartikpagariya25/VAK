using System.Net.Http;
using System.Windows.Threading;
using VAK.Core.Weather;

namespace VAK.App.Services;

/// <summary>Fetches the forecast for the saved city from Open-Meteo and refreshes it every 15 minutes while any view is using it.</summary>
public sealed class WeatherService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };
    private readonly AppHost _host;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMinutes(15) };
    private DateTime _lastFetch = DateTime.MinValue;
    private bool _busy;

    public WeatherReport? Report { get; private set; }

    /// <summary>True when the last refresh failed (offline or the service is down); the last good report is still shown.</summary>
    public bool Failed { get; private set; }

    public event Action? Changed;

    public WeatherService(AppHost host)
    {
        _host = host;
        _timer.Tick += async (_, _) => await RefreshAsync(force: true);
    }

    public bool HasCity => !string.IsNullOrWhiteSpace(_host.Settings.Weather.City);

    /// <summary>Starts the refresh timer and loads data if it is missing or older than 10 minutes.</summary>
    public async Task EnsureFreshAsync()
    {
        if (!_timer.IsEnabled) _timer.Start();
        await RefreshAsync(force: false);
    }

    public async Task RefreshAsync(bool force)
    {
        if (_busy || !HasCity) return;
        if (!force && Report is not null && DateTime.UtcNow - _lastFetch < TimeSpan.FromMinutes(10)) return;
        _busy = true;
        try
        {
            var w = _host.Settings.Weather;
            var json = await Http.GetStringAsync(OpenMeteo.ForecastUrl(w.Latitude, w.Longitude)).ConfigureAwait(true);
            var report = OpenMeteo.ParseForecast(json, DateTime.Now);
            Failed = report is null;
            if (report is not null) { Report = report; _lastFetch = DateTime.UtcNow; }
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { Failed = true; }
        finally { _busy = false; }
        Changed?.Invoke();
    }

    public async Task<IReadOnlyList<GeoPlace>> SearchAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return Array.Empty<GeoPlace>();
        try
        {
            var json = await Http.GetStringAsync(OpenMeteo.GeocodeUrl(name.Trim())).ConfigureAwait(true);
            return OpenMeteo.ParseGeocode(json) ?? Array.Empty<GeoPlace>();
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { return Array.Empty<GeoPlace>(); }
    }

    public async Task SetCityAsync(GeoPlace place)
    {
        var w = _host.Settings.Weather;
        w.City = place.Name; w.Latitude = place.Latitude; w.Longitude = place.Longitude;
        _host.Save();
        Report = null;
        Changed?.Invoke();
        await RefreshAsync(force: true);
    }
}
