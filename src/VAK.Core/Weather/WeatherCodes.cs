using System.Globalization;

namespace VAK.Core.Weather;

public static class WeatherCodes
{
    /// <summary>Maps a WMO weather code to its description and a VAK icon key (see the app's Icons map).</summary>
    public static (string Text, string Icon) Describe(int code, bool isDay) => code switch
    {
        0 => ("Clear Sky", isDay ? "sunny" : "moon"),
        1 => ("Mainly Clear", isDay ? "sunny" : "moon"),
        2 => ("Partly Cloudy", isDay ? "partly-day" : "partly-night"),
        3 => ("Overcast", "cloudy"),
        45 or 48 => ("Fog", "fog"),
        51 or 53 or 55 or 56 or 57 => ("Drizzle", "rain"),
        61 or 63 or 65 or 66 or 67 => ("Rain", "rain"),
        71 or 73 or 75 or 77 => ("Snow", "snow"),
        80 or 81 or 82 => ("Rain Showers", "rain"),
        85 or 86 => ("Snow Showers", "snow"),
        95 => ("Thunderstorm", "storm"),
        96 or 99 => ("Thunderstorm with Hail", "storm"),
        _ => ("Unknown", "cloudy"),
    };

    public static string UvLabel(double uv) => uv switch
    {
        < 3 => "Low",
        < 6 => "Moderate",
        < 8 => "High",
        < 11 => "Very High",
        _ => "Extreme",
    };

    /// <summary>Wind speed as "12 km/h" or, when asked, "7 mph".</summary>
    public static string FormatWind(double kmh, bool mph)
        => $"{Math.Round(mph ? kmh / 1.609344 : kmh, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture)} {(mph ? "mph" : "km/h")}";

    /// <summary>Rounds to a whole degree and appends the degree sign; converts to Fahrenheit when asked.</summary>
    public static string FormatTemp(double celsius, bool fahrenheit)
    {
        var value = fahrenheit ? celsius * 9 / 5 + 32 : celsius;
        var rounded = Math.Round(value, MidpointRounding.AwayFromZero);
        if (rounded == 0) rounded = 0; // never "-0"
        return rounded.ToString("0", CultureInfo.InvariantCulture) + "°";
    }
}
