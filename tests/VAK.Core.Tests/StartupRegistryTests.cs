using Microsoft.Win32;
using VAK.Core.Startup;
using Xunit;

namespace VAK.Core.Tests;

public class StartupRegistryTests : IDisposable
{
    private readonly string _subKey = @"Software\VAK-Tests\" + Guid.NewGuid().ToString("N");

    public void Dispose() => Registry.CurrentUser.DeleteSubKeyTree(_subKey, false);

    [Fact]
    public void Default_IsDisabled() =>
        Assert.False(new StartupRegistry(_subKey, "VAK", @"C:\Apps\VAK.exe").IsEnabled);

    [Fact]
    public void Enable_WritesQuotedExePath_AndReadsBack()
    {
        var s = new StartupRegistry(_subKey, "VAK", @"C:\Apps\VAK.exe");
        s.Set(true);
        Assert.True(s.IsEnabled);
        using var key = Registry.CurrentUser.OpenSubKey(_subKey);
        Assert.Equal("\"" + @"C:\Apps\VAK.exe" + "\"", key!.GetValue("VAK"));
    }

    [Fact]
    public void Disable_RemovesValue()
    {
        var s = new StartupRegistry(_subKey, "VAK", @"C:\Apps\VAK.exe");
        s.Set(true);
        s.Set(false);
        Assert.False(s.IsEnabled);
    }

    [Fact]
    public void Disable_WhenNeverEnabled_DoesNotThrow() =>
        new StartupRegistry(_subKey, "VAK", @"C:\Apps\VAK.exe").Set(false);
}
