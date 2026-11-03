using VAK.Core.Units;
using Xunit;

namespace VAK.Core.Tests;

public class UnitConverterTests
{
    private static UnitDef U(string category, string id) =>
        UnitCatalog.Find(category)!.Units.Single(u => u.Id == id);

    [Fact]
    public void OneMeter_IsAbout3_28084Feet() =>
        Assert.Equal(3.28084, UnitConverter.Convert(1, U("length", "m"), U("length", "ft")), 5);

    [Fact]
    public void HundredCentimeters_IsOneMeter() =>
        Assert.Equal(1, UnitConverter.Convert(100, U("length", "cm"), U("length", "m")), 9);

    [Theory]
    [InlineData("C", "F", 0, 32)]
    [InlineData("K", "C", 273.15, 0)]
    [InlineData("F", "C", 212, 100)]
    public void Temperature_UsesOffsets(string from, string to, double value, double expected) =>
        Assert.Equal(expected, UnitConverter.Convert(value, U("temperature", from), U("temperature", to)), 6);

    [Fact]
    public void OneGigabyte_Is1024Megabytes() =>
        Assert.Equal(1024, UnitConverter.Convert(1, U("data", "GB"), U("data", "MB")), 6);

    [Fact]
    public void OneHour_Is3600Seconds() =>
        Assert.Equal(3600, UnitConverter.Convert(1, U("time", "h"), U("time", "s")), 9);

    [Fact]
    public void SixtyKmPerHour_IsAbout16_6667MetersPerSecond() =>
        Assert.Equal(16.6667, UnitConverter.Convert(60, U("speed", "kmh"), U("speed", "ms")), 4);

    [Theory]
    [InlineData(3.280839895, "3.28084")]
    [InlineData(100, "100")]
    [InlineData(0.5, "0.5")]
    [InlineData(1e-9, "0")]
    [InlineData(1234567.891, "1234567.891")]
    public void Format_TrimsToSixDecimals(double value, string expected) =>
        Assert.Equal(expected, UnitConverter.Format(value));

    [Fact]
    public void Catalog_HasSevenOfflineCategories() =>
        Assert.Equal(new[] { "length", "mass", "temperature", "data", "volume", "speed", "time" }, UnitCatalog.All.Select(c => c.Id));
}
