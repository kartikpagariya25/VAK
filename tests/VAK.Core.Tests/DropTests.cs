using System.Net;
using VAK.Core.Drop;
using Xunit;

namespace VAK.Core.Tests;

public class DropTests
{
    [Theory]
    [InlineData("192.168.1.20", true)]
    [InlineData("10.0.0.5", true)]
    [InlineData("172.16.0.1", true)]
    [InlineData("172.31.255.254", true)]
    [InlineData("172.32.0.1", false)]
    [InlineData("8.8.8.8", false)]
    [InlineData("169.254.10.10", false)]
    [InlineData("127.0.0.1", false)]
    public void IsPrivate_RecognisesLanRanges(string ip, bool expected) =>
        Assert.Equal(expected, PrivateNetwork.IsPrivate(IPAddress.Parse(ip)));

    [Fact]
    public void IsPrivate_RejectsIPv6() => Assert.False(PrivateNetwork.IsPrivate(IPAddress.Parse("fe80::1")));

    [Fact]
    public void Pick_PrefersTheFirstPrivateAddress_AndSkipsPublicOnes()
    {
        var picked = PrivateNetwork.Pick(new[] { IPAddress.Parse("8.8.8.8"), IPAddress.Parse("192.168.1.7"), IPAddress.Parse("10.0.0.2") });
        Assert.Equal(IPAddress.Parse("192.168.1.7"), picked);
        Assert.Null(PrivateNetwork.Pick(new[] { IPAddress.Parse("8.8.8.8") }));
    }

    [Theory]
    [InlineData("photo.jpg", "photo.jpg")]
    [InlineData(@"C:\Users\me\photo.jpg", "photo.jpg")]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("a:b*c?.txt", "abc.txt")]
    [InlineData("   ", "file")]
    [InlineData("", "file")]
    [InlineData("..", "file")]
    [InlineData("CON", "file")]
    [InlineData("nul.txt", "file.txt")]
    public void SafeFileName_StripsPathsAndReservedNames(string input, string expected) =>
        Assert.Equal(expected, UploadNames.Safe(input));

    [Fact]
    public void SafeFileName_IsLimitedTo120Chars() =>
        Assert.True(UploadNames.Safe(new string('a', 300) + ".txt").Length <= 120);

    [Fact]
    public void Token_Is32HexChars_AndDifferentEachTime()
    {
        var a = DropToken.New();
        Assert.Matches("^[0-9a-f]{32}$", a);
        Assert.NotEqual(a, DropToken.New());
    }

    [Theory]
    [InlineData("abc", "abc", true)]
    [InlineData("abc", "abd", false)]
    [InlineData("abc", "", false)]
    [InlineData("abc", null, false)]
    [InlineData("abc", "abcd", false)]
    public void Token_Matches_IsExact(string expected, string? given, bool ok) =>
        Assert.Equal(ok, DropToken.Matches(expected, given));
}
