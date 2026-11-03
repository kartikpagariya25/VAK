using VAK.Core.Weather;
using Xunit;

namespace VAK.Core.Tests;

public class WeatherTests
{
    private const string Forecast = @"{
      ""current"": {""time"":""2026-11-03T18:00"",""temperature_2m"":30.2,""apparent_temperature"":33.4,""relative_humidity_2m"":57,
                    ""weather_code"":0,""wind_speed_10m"":3.1,""is_day"":0,""uv_index"":0.0},
      ""hourly"": {""time"":[""2026-11-03T17:00"",""2026-11-03T18:00"",""2026-11-03T19:00"",""2026-11-03T20:00""],
                   ""temperature_2m"":[31,30,29,28],""weather_code"":[0,0,1,3]},
      ""daily"": {""time"":[""2026-11-03"",""2026-11-04""],""weather_code"":[2,3],""temperature_2m_max"":[32.1,33],
                  ""temperature_2m_min"":[23,22.4],""sunrise"":[""2026-11-03T06:32"",""2026-11-04T06:33""],
                  ""sunset"":[""2026-11-03T17:52"",""2026-11-04T17:51""],""uv_index_max"":[6.2,5]}
    }";

    private const string Geo = @"{""results"":[
      {""name"":""Dharangaon"",""admin1"":""Maharashtra"",""country"":""India"",""latitude"":21.0167,""longitude"":75.2833},
      {""name"":""Dhar"",""country"":""India"",""latitude"":22.6,""longitude"":75.3}]}";

    [Fact]
    public void ParseForecast_ReadsCurrentConditions()
    {
        var r = OpenMeteo.ParseForecast(Forecast, new DateTime(2026, 11, 3, 18, 1, 0))!;
        Assert.Equal(30.2, r.Now.Temp);
        Assert.Equal(33.4, r.Now.Feels);
        Assert.Equal(57, r.Now.Humidity);
        Assert.Equal(3.1, r.Now.WindKmh);
        Assert.False(r.Now.IsDay);
        Assert.Equal(new DateTime(2026, 11, 3, 18, 0, 0), r.Now.Time);
    }

    [Fact]
    public void ParseForecast_ReadsDailyWithSunTimes()
    {
        var d = OpenMeteo.ParseForecast(Forecast, DateTime.Now)!.Daily;
        Assert.Equal(2, d.Count);
        Assert.Equal(32.1, d[0].Max);
        Assert.Equal(23, d[0].Min);
        Assert.Equal(new DateTime(2026, 11, 3, 6, 32, 0), d[0].Sunrise);
        Assert.Equal(new DateTime(2026, 11, 3, 17, 52, 0), d[0].Sunset);
    }

    [Fact]
    public void NextHours_StartsAtTheCurrentHour()
    {
        var r = OpenMeteo.ParseForecast(Forecast, DateTime.Now)!;
        var next = OpenMeteo.NextHours(r, 3);
        Assert.Equal(new[] { 30.0, 29.0, 28.0 }, next.Select(h => h.Temp));
    }

    [Theory]
    [InlineData("")]
    [InlineData("nope")]
    [InlineData("{}")]
    [InlineData("{\"current\":{}}")]
    public void ParseForecast_BadInput_ReturnsNull(string json) =>
        Assert.Null(OpenMeteo.ParseForecast(json, DateTime.Now));

    [Fact]
    public void ParseGeocode_ReadsPlaces()
    {
        var places = OpenMeteo.ParseGeocode(Geo)!;
        Assert.Equal(2, places.Count);
        Assert.Equal("Dharangaon", places[0].Name);
        Assert.Equal("Dharangaon, Maharashtra, India", places[0].Display);
        Assert.Equal("Dhar, India", places[1].Display);
        Assert.Equal(75.2833, places[0].Longitude);
    }

    [Fact]
    public void ParseGeocode_NoResults_ReturnsEmpty() =>
        Assert.Empty(OpenMeteo.ParseGeocode("{}")!);

    [Theory]
    [InlineData(0, true, "Clear Sky", "sunny")]
    [InlineData(0, false, "Clear Sky", "moon")]
    [InlineData(2, true, "Partly Cloudy", "partly-day")]
    [InlineData(2, false, "Partly Cloudy", "partly-night")]
    [InlineData(3, true, "Overcast", "cloudy")]
    [InlineData(45, true, "Fog", "fog")]
    [InlineData(63, true, "Rain", "rain")]
    [InlineData(73, true, "Snow", "snow")]
    [InlineData(95, true, "Thunderstorm", "storm")]
    [InlineData(1234, true, "Unknown", "cloudy")]
    public void Describe_MapsWmoCodes(int code, bool day, string text, string icon) =>
        Assert.Equal((text, icon), WeatherCodes.Describe(code, day));

    [Theory]
    [InlineData(0, "Low")]
    [InlineData(2.9, "Low")]
    [InlineData(3, "Moderate")]
    [InlineData(6, "High")]
    [InlineData(8, "Very High")]
    [InlineData(11, "Extreme")]
    public void UvLabel_Bands(double uv, string expected) => Assert.Equal(expected, WeatherCodes.UvLabel(uv));

    [Theory]
    [InlineData(30, false, "30°")]
    [InlineData(30.6, false, "31°")]
    [InlineData(0, true, "32°")]
    [InlineData(-0.2, false, "0°")]
    public void FormatTemp_RoundsAndConverts(double celsius, bool fahrenheit, string expected) =>
        Assert.Equal(expected, WeatherCodes.FormatTemp(celsius, fahrenheit));
}
