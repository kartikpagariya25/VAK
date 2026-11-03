using VAK.Core.Settings;
using VAK.Core.Sys;
using VAK.Core.Widgets;
using VAK.App.Shell;
using VAK.App.Services;
using VAK.App.Widgets;

namespace VAK.App;

/// <summary>Everything the UI shares: settings, the widget registry, navigation and the system services.</summary>
public sealed class AppHost
{
    private readonly SettingsStore _store;

    public AppSettings Settings { get; }
    public WidgetRegistry Registry { get; } = new();
    public Dictionary<string, WidgetModule> Modules { get; } = new();
    public Navigator Nav { get; } = new();

    public IBatteryService Battery { get; } = new WindowsBatteryService();
    public INetworkService Network { get; } = new WindowsNetworkService();
    public IPerformanceService Performance { get; } = new WindowsPerformanceService();
    public IVolumeService Volume { get; } = new WindowsVolumeService();

    public Core.Startup.StartupRegistry Startup { get; } =
        new(Core.Startup.StartupRegistry.RunKeyPath, "VAK", Environment.ProcessPath ?? "");

    /// <summary>Raised when Start with Windows changes from anywhere (Settings page or tray menu).</summary>
    public event Action<bool>? StartWithWindowsChanged;

    /// <summary>Set by the app shell: tries to register a new global hotkey and returns false if another app holds it.</summary>
    public Func<string, bool>? TryApplyHotkey { get; set; }

    public void SetStartWithWindows(bool enabled)
    {
        try { Startup.Set(enabled); }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or System.IO.IOException) { }
        enabled = Startup.IsEnabled; // report what actually happened, so a denied registry write shows as unchanged
        Settings.StartWithWindows = enabled;
        Save();
        StartWithWindowsChanged?.Invoke(enabled);
    }

    public SystemMonitor Monitor { get; }
    public TimersService Timers { get; }

    public AppHost(string settingsPath)
    {
        Monitor = new SystemMonitor(this);
        _store = new SettingsStore(settingsPath);
        Settings = _store.Load();
        Settings.StartWithWindows = Startup.IsEnabled; // the Run key is the source of truth
        if (!Settings.WorldClocksInitialized)
        {
            Settings.WorldClocks.AddRange(new[]
            {
                new Core.Settings.WorldClockEntry("New York", "Eastern Standard Time"),
                new Core.Settings.WorldClockEntry("London", "GMT Standard Time"),
                new Core.Settings.WorldClockEntry("Tokyo", "Tokyo Standard Time"),
            });
            Settings.WorldClocksInitialized = true;
        }
        Timers = new TimersService(this);
        foreach (var module in WidgetCatalog.Build(this))
        {
            Registry.Register(module.Info);
            Modules[module.Info.Id] = module;
        }
    }

    public void Save() => _store.Save(Settings);
}
