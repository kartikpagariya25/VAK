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

    public SystemMonitor Monitor { get; }

    public AppHost(string settingsPath)
    {
        Monitor = new SystemMonitor(this);
        _store = new SettingsStore(settingsPath);
        Settings = _store.Load();
        foreach (var module in WidgetCatalog.Build(this))
        {
            Registry.Register(module.Info);
            Modules[module.Info.Id] = module;
        }
    }

    public void Save() => _store.Save(Settings);
}
