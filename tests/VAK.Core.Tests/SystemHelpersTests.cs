using VAK.Core.Sys;
using Xunit;

namespace VAK.Core.Tests;

public class SystemHelpersTests
{
    [Fact]
    public void Sparkline_DropsOldest()
    {
        var b = new SparklineBuffer(3);
        for (var i = 1; i <= 5; i++) b.Add(i);
        Assert.Equal(new double[] { 3, 4, 5 }, b.Values);
    }

    [Fact]
    public void Rate_Normal() =>
        Assert.Equal(1000, RateCalculator.BytesPerSecond(1000, 3000, TimeSpan.FromSeconds(2)));

    [Fact]
    public void Rate_CounterReset_Returns0() =>
        Assert.Equal(0, RateCalculator.BytesPerSecond(5000, 100, TimeSpan.FromSeconds(1)));

    [Fact]
    public void Rate_ZeroElapsed_Returns0() =>
        Assert.Equal(0, RateCalculator.BytesPerSecond(1, 100, TimeSpan.Zero));

    [Fact]
    public void Health_88_Of_Design() =>
        Assert.Equal(88, BatteryMath.HealthPercent(44000, 50000));

    [Theory]
    [InlineData(null, 50000)]
    [InlineData(44000, null)]
    [InlineData(44000, 0)]
    public void Health_NullOrZeroInputs_ReturnsNull(int? full, int? design) =>
        Assert.Null(BatteryMath.HealthPercent(full, design));

    [Fact]
    public void Health_Clamps_At100() =>
        Assert.Equal(100, BatteryMath.HealthPercent(60000, 50000));
}
