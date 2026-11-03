namespace VAK.Core.Weather;

public record WeatherNow(DateTime Time, double Temp, double Feels, int Humidity, double WindKmh, double Uv, int Code, bool IsDay);
public record HourPoint(DateTime Time, double Temp, int Code);
public record DayPoint(DateTime Date, double Min, double Max, int Code, DateTime? Sunrise, DateTime? Sunset);
public record WeatherReport(WeatherNow Now, IReadOnlyList<HourPoint> Hourly, IReadOnlyList<DayPoint> Daily, DateTime Updated);

public record GeoPlace(string Name, string? Region, string? Country, double Latitude, double Longitude)
{
    /// <summary>"Dharangaon, Maharashtra, India" (missing parts are skipped).</summary>
    public string Display => string.Join(", ", new[] { Name, Region, Country }.Where(s => !string.IsNullOrWhiteSpace(s)));
}
