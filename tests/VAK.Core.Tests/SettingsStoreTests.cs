using VAK.Core.Settings;
using Xunit;

namespace VAK.Core.Tests;

public class SettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vak-tests-" + Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_dir, "settings.json");

    public SettingsStoreTests() => Directory.CreateDirectory(_dir);
    public void Dispose() => Directory.Delete(_dir, true);

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var s = new SettingsStore(FilePath).Load();
        Assert.Equal("Ctrl+Alt+Space", s.Hotkey);
        Assert.Equal(25, s.Timers.FocusMinutes);
        Assert.Equal(6, s.HomeSlots.Length);
    }

    [Fact]
    public void Save_Then_Load_RoundTrips()
    {
        var store = new SettingsStore(FilePath);
        var s = new AppSettings { Use24Hour = true, WaterToday = 3 };
        store.Save(s);
        var loaded = store.Load();
        Assert.True(loaded.Use24Hour);
        Assert.Equal(3, loaded.WaterToday);
    }

    [Fact]
    public void Load_CorruptFile_ReturnsDefaultsAndKeepsBak()
    {
        File.WriteAllText(FilePath, "{\"Hotkey\":");
        var s = new SettingsStore(FilePath).Load();
        Assert.Equal("Ctrl+Alt+Space", s.Hotkey);
        Assert.Equal("{\"Hotkey\":", File.ReadAllText(FilePath + ".bak"));
    }

    [Fact]
    public void Load_EmptyFile_ReturnsDefaults()
    {
        File.WriteAllText(FilePath, "");
        var s = new SettingsStore(FilePath).Load();
        Assert.Equal(25, s.Timers.FocusMinutes);
    }

    [Fact]
    public void Load_UnknownFields_Ignored()
    {
        File.WriteAllText(FilePath, "{\"Use24Hour\":true,\"FutureThing\":{\"a\":1}}");
        var s = new SettingsStore(FilePath).Load();
        Assert.True(s.Use24Hour);
    }

    [Theory]
    [InlineData("{\"Dock\":null}")]
    [InlineData("{\"Clock\":null}")]
    [InlineData("{\"Timers\":null}")]
    [InlineData("{\"WorldClocks\":null}")]
    [InlineData("{\"HomeSlots\":null}")]
    public void Load_ExplicitNulls_FallBackToDefaults(string json)
    {
        File.WriteAllText(FilePath, json);
        var s = new SettingsStore(FilePath).Load();
        Assert.NotNull(s.Dock);
        Assert.NotNull(s.Clock);
        Assert.NotNull(s.Timers);
        Assert.NotNull(s.WorldClocks);
        Assert.Equal(6, s.HomeSlots.Length);
        Assert.Equal(25, s.Timers.FocusMinutes);
    }

    [Fact]
    public void Save_UnwritableLocation_DoesNotThrow()
    {
        var blocker = Path.Combine(_dir, "blocker");
        File.WriteAllText(blocker, "x"); // a file where a directory is needed
        new SettingsStore(Path.Combine(blocker, "settings.json")).Save(new AppSettings());
    }
}
