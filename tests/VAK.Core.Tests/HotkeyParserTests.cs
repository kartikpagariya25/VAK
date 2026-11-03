using VAK.Core.Hotkeys;
using Xunit;

namespace VAK.Core.Tests;

public class HotkeyParserTests
{
    [Fact]
    public void Parses_CtrlAltSpace() =>
        Assert.Equal((HotkeyParser.Ctrl | HotkeyParser.Alt, 0x20u), HotkeyParser.TryParse("Ctrl+Alt+Space"));

    [Fact]
    public void Parses_CaseInsensitive_WinShiftLetter() =>
        Assert.Equal((HotkeyParser.Win | HotkeyParser.Shift, 0x4Bu), HotkeyParser.TryParse("win+SHIFT+k"));

    [Fact]
    public void Parses_FunctionKey() =>
        Assert.Equal((HotkeyParser.Ctrl, 0x74u), HotkeyParser.TryParse("Ctrl+F5"));

    [Theory]
    [InlineData("Space")]
    [InlineData("Ctrl+")]
    [InlineData("Ctrl+Alt+Banana")]
    [InlineData("")]
    public void Invalid_ReturnsNull(string text) => Assert.Null(HotkeyParser.TryParse(text));
}
