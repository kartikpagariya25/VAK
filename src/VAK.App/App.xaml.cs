using System.IO;
using System.Windows;
using VAK.App.Services;
using VAK.App.Shell;
using VAK.Core.Settings;
using VAK.Core.Sys;

namespace VAK.App;

public partial class App : Application
{
    private Mutex? _singleInstance;
    private ShellWindow? _shell;
    private HotkeyService? _hotkey;
    private TrayService? _tray;
    private ClipboardMonitor? _clipboard;
    private ScreenTimeTracker? _screenTime;
    private AppHost? _host;
    private System.Windows.Threading.DispatcherTimer? _dropGuard;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            LogError(args.Exception);
            args.Handled = true; // a tray app that runs all day should survive a bad click or tick
        };
        _singleInstance = new Mutex(true, "VAK.SingleInstance", out var first);
        if (!first) { Shutdown(); return; }

        if (e.Args.Contains("--gallery")) { new Theme.DesignGalleryWindow().Show(); return; }

        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VAK", "settings.json");
        var host = _host = new AppHost(path);
        var settings = host.Settings;

        host.ClipboardMonitor = _clipboard = new ClipboardMonitor(host.Clipboard, host.DataDir);
        host.ScreenTimeTracker = _screenTime = new ScreenTimeTracker(host.ScreenTime);
        _shell = new ShellWindow(host);
        var leftArg = Array.IndexOf(e.Args, "--left");
        if (leftArg >= 0 && leftArg + 1 < e.Args.Length && double.TryParse(e.Args[leftArg + 1], out var left))
            _shell.LeftOverride = left;
        _shell.PositionTopCenter();
        _shell.Show();

        _tray = new TrayService(settings.StartWithWindows);
        _tray.ShowHideRequested += () => _shell.Toggle();
        _tray.QuitRequested += Shutdown;
        _tray.StartWithWindowsChanged += host.SetStartWithWindows;
        host.StartWithWindowsChanged += _tray.SetStartWithWindows;

        host.Timers.Notice += message =>
        {
            System.Media.SystemSounds.Asterisk.Play();
            _tray.ShowNotice("VAK", message);
        };

        // Drop is only allowed while the network is private. This watches for the network changing (a laptop moving to a cafe),
        // switches Drop off when it does, tells the user, and shows in the tray tooltip whenever Drop is on.
        host.Drop.Changed += () => Dispatcher.Invoke(() => _tray.SetTooltip(host.Drop.Running ? "VAK - Drop is on" : "VAK"));
        host.Drop.AutoStopped += () => Dispatcher.Invoke(() => _tray.ShowNotice("VAK Drop", "Drop switched itself off after sitting idle."));
        _dropGuard = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _dropGuard.Tick += async (_, _) =>
        {
            if (host.Drop.Running is false || host.Drop.Url is not { } bound) return;
            var (state, address) = await Task.Run(NetworkCategory.Check);
            if (host.Drop.Running && (state != NetworkState.Private || address?.ToString() != new Uri(bound).Host))
            {
                await host.Drop.StopAsync();
                _tray.ShowNotice("VAK Drop", "Drop was switched off because the network changed.");
            }
        };
        _dropGuard.Start();

        host.Calendar.Reminder += o =>
        {
            var lead = o.Event.ReminderMinutes;
            var when = lead <= 0 ? "starting now" : lead >= 60 ? "in 1 hour" : $"in {lead} min";
            var text = $"{o.Event.Title} · {when}";
            System.Media.SystemSounds.Asterisk.Play();
            _tray.ShowNotice("Calendar", text);
            if (Core.Message.BannerSpec.TryCreate(text, Core.Message.BannerAnimation.Slide, 1, o.Event.Color, Core.Message.BannerFinish.Solid, 10, false) is { } banner)
                host.FlashBanner(banner);
        };

        _hotkey = new HotkeyService(_shell);
        _hotkey.Pressed += () => _shell.Toggle();
        host.TryApplyHotkey = hk => _hotkey.Register(hk);
        if (!_hotkey.Register(settings.Hotkey))
            _tray.ShowNotice("VAK", $"The hotkey {settings.Hotkey} is already in use. Hover the top of the screen instead, or change it in Settings.");
    }

    private static void LogError(Exception ex)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VAK");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "error.log"), $"{DateTime.Now:O}\n{ex}\n\n");
        }
        catch { /* logging must never throw */ }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkey?.Dispose();
        _clipboard?.Dispose();
        _screenTime?.Flush();
        _dropGuard?.Stop();
        _host?.Drop.StopAsync().Wait(TimeSpan.FromSeconds(2));
        _tray?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
