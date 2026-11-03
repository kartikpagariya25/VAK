using VAK.Core.Sys;
using Xunit;

namespace VAK.Core.Tests;

public class RateFormatterTests
{
    [Theory]
    [InlineData(0, "0 KB/s")]
    [InlineData(500, "0 KB/s")]
    [InlineData(9216, "9 KB/s")]
    [InlineData(1572864, "1.5 MB/s")]
    [InlineData(10485760, "10 MB/s")]
    public void Format_PicksUnit(double bytesPerSecond, string expected) =>
        Assert.Equal(expected, RateFormatter.Format(bytesPerSecond));
}
