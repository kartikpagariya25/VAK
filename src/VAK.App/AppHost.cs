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

    /// <summary>Opens the Settings window, optionally on a section ("general", "notch", ...). Set by the app.</summary>
    public Action<string?>? OpenSettings { get; set; }

    /// <summary>Set by the app shell: tries to register a new global hotkey and returns false if another app holds it.</summary>
    public Func<string, bool>? TryApplyHotkey { get; set; }

    /// <summary>The Message page asks the notch to show or clear a banner through these.</summary>
    public event Action<Core.Message.BannerSpec>? BannerRequested;
    public event Action? BannerCleared;

    /// <summary>Raised after the Settings window changes something the shell has to re-apply (notch, theme, live activities, dock).</summary>
    public event Action? SettingsApplied;

    /// <summary>Raised after the theme changed, so windows and pages that fetched brushes in code can rebuild.</summary>
    public event Action? ThemeChanged;
    public void RaiseThemeChanged() => ThemeChanged?.Invoke();

    /// <summary>Saves, then tells the shell to re-read the settings.</summary>
    public void Apply() { Save(); SettingsApplied?.Invoke(); }

    /// <summary>Raised when the dock or Home widgets changed, so the open panel rebuilds them.</summary>
    public event Action? ModulesChanged;
    public void RaiseModulesChanged() => ModulesChanged?.Invoke();
    public void FlashBanner(Core.Message.BannerSpec spec) => BannerRequested?.Invoke(spec);
    public void ClearBanner() => BannerCleared?.Invoke();

    public void SetStartWithWindows(bool enabled)
    {
        try { Startup.Set(enabled); }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or System.IO.IOException) { }
        enabled = Startup.IsEnabled; // report what actually happened, so a denied registry write shows as unchanged
        Settings.StartWithWindows = enabled;
        Save();
        StartWithWindowsChanged?.Invoke(enabled);
    }

    public NowPlayingService NowPlaying { get; } = new();
    public LiveActivities LiveActivities { get; set; } = null!;

    public SystemMonitor Monitor { get; }
    public TimersService Timers { get; }

    /// <summary>Folder for everything VAK stores (settings, notes, clipboard, rates...).</summary>
    public string DataDir { get; }
    public CurrencyService Currency { get; }
    public WeatherService Weather { get; }
    public Core.Notes.NotesRepository Notes { get; }
    public Core.Links.LinksRepository Links { get; }
    public Core.Clipboard.ClipboardHistory Clipboard { get; }
    public Core.ScreenTime.ScreenTimeLog ScreenTime { get; }
    public Core.Shelf.ShelfStore Shelf { get; }
    public ConverterService Converter { get; }
    public CalendarService Calendar { get; }
    public AiWebViews AiWeb { get; }
    public Core.Chat.ApiKeyVault ApiKeys { get; }
    public Core.Chat.ChatStore Chats { get; }
    public Core.Chat.ChatClient ChatApi { get; } = new();
    public Core.Drop.DropServer Drop { get; } = new(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "VAK Drop"));
    public ScreenTimeTracker? ScreenTimeTracker { get; set; }

    /// <summary>Started by the app shell once the UI is up; null until then.</summary>
    public ClipboardMonitor? ClipboardMonitor { get; set; }

    public AppHost(string settingsPath)
    {
        DataDir = System.IO.Path.GetDirectoryName(settingsPath)!;
        Currency = new CurrencyService(DataDir);
        Weather = new WeatherService(this);
        Notes = new Core.Notes.NotesRepository(System.IO.Path.Combine(DataDir, "notes.json"), () => DateTime.Now);
        Links = new Core.Links.LinksRepository(System.IO.Path.Combine(DataDir, "links.json"));
        Shelf = new Core.Shelf.ShelfStore(System.IO.Path.Combine(DataDir, "shelves"));
        ScreenTime = new Core.ScreenTime.ScreenTimeLog(System.IO.Path.Combine(DataDir, "screentime.json"), () => DateTime.Now);
        Clipboard = new Core.Clipboard.ClipboardHistory(System.IO.Path.Combine(DataDir, "clipboard.json"), () => DateTime.Now);
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
        Converter = new ConverterService(this);
        Calendar = new CalendarService(DataDir);
        AiWeb = new AiWebViews(DataDir);
        ApiKeys = new Core.Chat.ApiKeyVault(System.IO.Path.Combine(DataDir, "apikeys.dat"));
        Chats = new Core.Chat.ChatStore(System.IO.Path.Combine(DataDir, "chats.json"));
        foreach (var module in WidgetCatalog.Build(this))
        {
            Registry.Register(module.Info);
            Modules[module.Info.Id] = module;
        }
    }

    public void Save() => _store.Save(Settings);
}
