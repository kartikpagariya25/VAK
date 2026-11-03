using VAK.Core.Message;
using Xunit;

namespace VAK.Core.Tests;

public class BannerSpecTests
{
    [Fact]
    public void TryCreate_TrimsAndKeepsSettings()
    {
        var b = BannerSpec.TryCreate("  Lunch time  ", BannerAnimation.Fade, 1.5, "#F5A623", BannerFinish.Ocean, 10, false)!;
        Assert.Equal("Lunch time", b.Text);
        Assert.Equal((BannerAnimation.Fade, 1.5, "#F5A623", BannerFinish.Ocean, 10, false),
            (b.Animation, b.Speed, b.ColorHex, b.Finish, b.DurationSeconds, b.Repeat));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TryCreate_BlankText_ReturnsNull(string text) =>
        Assert.Null(BannerSpec.TryCreate(text, BannerAnimation.Slide, 1, "#FFFFFF", BannerFinish.Solid, 5, false));

    [Fact]
    public void TryCreate_LongText_IsCutTo60() =>
        Assert.Equal(60, BannerSpec.TryCreate(new string('x', 100), BannerAnimation.Slide, 1, "#FFFFFF", BannerFinish.Solid, 5, false)!.Text.Length);

    [Theory]
    [InlineData(0.1, 0.5)]
    [InlineData(1, 1)]
    [InlineData(9, 2)]
    public void Speed_IsClampedTo_0_5_And_2(double input, double expected) =>
        Assert.Equal(expected, BannerSpec.TryCreate("x", BannerAnimation.Slide, input, "#FFFFFF", BannerFinish.Solid, 5, false)!.Speed);

    [Theory]
    [InlineData(0, 1)]
    [InlineData(5, 5)]
    [InlineData(500, 60)]
    public void Duration_IsClampedTo_1_And_60(int input, int expected) =>
        Assert.Equal(expected, BannerSpec.TryCreate("x", BannerAnimation.Slide, 1, "#FFFFFF", BannerFinish.Solid, input, false)!.DurationSeconds);

    [Theory]
    [InlineData("#abc")]
    [InlineData("red")]
    [InlineData("")]
    [InlineData("#GGGGGG")]
    public void BadColor_FallsBackToWhite(string color) =>
        Assert.Equal("#FFFFFF", BannerSpec.TryCreate("x", BannerAnimation.Slide, 1, color, BannerFinish.Solid, 5, false)!.ColorHex);

    [Fact]
    public void ValidColor_IsUpperCased() =>
        Assert.Equal("#F5A623", BannerSpec.TryCreate("x", BannerAnimation.Slide, 1, "#f5a623", BannerFinish.Solid, 5, false)!.ColorHex);

    [Fact]
    public void Palette_HasSevenColors() => Assert.Equal(7, BannerPalette.Colors.Count);
}
