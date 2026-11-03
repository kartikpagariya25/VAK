using System.Globalization;
using System.Text.Json;

namespace VAK.Core.Weather;

/// <summary>URLs for and parsing of the Open-Meteo forecast and geocoding APIs (free, no key).</summary>
public static class OpenMeteo
{
    public static string ForecastUrl(double latitude, double longitude) =>
        "https://api.open-meteo.com/v1/forecast" +
        $"?latitude={latitude.ToString(CultureInfo.InvariantCulture)}&longitude={longitude.ToString(CultureInfo.InvariantCulture)}" +
        "&current=temperature_2m,apparent_temperature,relative_humidity_2m,weather_code,wind_speed_10m,is_day,uv_index" +
        "&hourly=temperature_2m,weather_code" +
        "&daily=weather_code,temperature_2m_max,temperature_2m_min,sunrise,sunset,uv_index_max" +
        "&timezone=auto&forecast_days=6&wind_speed_unit=kmh";

    public static string GeocodeUrl(string name) =>
        "https://geocoding-api.open-meteo.com/v1/search?count=6&language=en&name=" + Uri.EscapeDataString(name);

    public static WeatherReport? ParseForecast(string json, DateTime updated)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("current", out var cur) ||
                !cur.TryGetProperty("temperature_2m", out var temp)) return null;

            var now = new WeatherNow(
                Time(cur, "time") ?? updated,
                temp.GetDouble(),
                Num(cur, "apparent_temperature") ?? temp.GetDouble(),
                (int)Math.Round(Num(cur, "relative_humidity_2m") ?? 0),
                Num(cur, "wind_speed_10m") ?? 0,
                Num(cur, "uv_index") ?? 0,
                (int)(Num(cur, "weather_code") ?? 0),
                (Num(cur, "is_day") ?? 1) >= 1);

            var hours = new List<HourPoint>();
            if (root.TryGetProperty("hourly", out var h) && h.TryGetProperty("time", out var ht))
            {
                var temps = h.GetProperty("temperature_2m");
                var codes = h.GetProperty("weather_code");
                for (var i = 0; i < ht.GetArrayLength(); i++)
                    if (ParseTime(ht[i].GetString()) is { } t && temps[i].ValueKind == JsonValueKind.Number)
                        hours.Add(new HourPoint(t, temps[i].GetDouble(), codes[i].ValueKind == JsonValueKind.Number ? codes[i].GetInt32() : 0));
            }

            var days = new List<DayPoint>();
            if (root.TryGetProperty("daily", out var d) && d.TryGetProperty("time", out var dt))
            {
                for (var i = 0; i < dt.GetArrayLength(); i++)
                    if (ParseTime(dt[i].GetString()) is { } date)
                        days.Add(new DayPoint(date,
                            d.GetProperty("temperature_2m_min")[i].GetDouble(), d.GetProperty("temperature_2m_max")[i].GetDouble(),
                            d.GetProperty("weather_code")[i].GetInt32(),
                            d.TryGetProperty("sunrise", out var sr) ? ParseTime(sr[i].GetString()) : null,
                            d.TryGetProperty("sunset", out var ss) ? ParseTime(ss[i].GetString()) : null));
            }
            return new WeatherReport(now, hours, days, updated);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException or IndexOutOfRangeException or FormatException)
        {
            return null;
        }
    }

    /// <summary>The next <paramref name="count"/> hourly points starting at the current hour.</summary>
    public static IReadOnlyList<HourPoint> NextHours(WeatherReport report, int count)
    {
        var start = new DateTime(report.Now.Time.Year, report.Now.Time.Month, report.Now.Time.Day, report.Now.Time.Hour, 0, 0);
        return report.Hourly.Where(p => p.Time >= start).Take(count).ToList();
    }

    public static IReadOnlyList<GeoPlace>? ParseGeocode(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            if (!doc.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
                return Array.Empty<GeoPlace>();
            var list = new List<GeoPlace>();
            foreach (var r in results.EnumerateArray())
            {
                if (!r.TryGetProperty("name", out var name) || Num(r, "latitude") is not { } lat || Num(r, "longitude") is not { } lon) continue;
                list.Add(new GeoPlace(name.GetString() ?? "", Str(r, "admin1"), Str(r, "country"), lat, lon));
            }
            return list;
        }
        catch (JsonException) { return null; }
    }

    private static double? Num(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static DateTime? Time(JsonElement e, string name) => Str(e, name) is { } s ? ParseTime(s) : null;

    private static DateTime? ParseTime(string? s) =>
        DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t : null;
}
