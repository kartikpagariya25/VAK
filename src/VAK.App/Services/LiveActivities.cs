using System.Windows.Threading;
using VAK.Core.Message;
using VAK.Core.Settings;

namespace VAK.App.Services;

/// <summary>Watches battery, volume, the clipboard, timers, calendar and the music player, and flashes a short banner on the notch
/// for each event the user left switched on in Settings > Live Activities.</summary>
public sealed class LiveActivities
{
    private readonly AppHost _host;
    private readonly DispatcherTimer _battery = new() { Interval = TimeSpan.FromSeconds(4) };
    private readonly DispatcherTimer _volume = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private Core.Sys.BatteryInfo? _lastBattery;
    private bool _batteryKnown, _fullAnnounced;
    private int _lastVolume = -1;
    private bool _lastMuted;
    private int _pendingVolume = -1;
    private bool _pendingMuted;
    private DateTime _volumeChangedAt = DateTime.MinValue;

    public LiveActivities(AppHost host)
    {
        _host = host;
        _battery.Tick += (_, _) => CheckBattery();
        _volume.Tick += (_, _) => CheckVolume();
    }

    private LiveSettings Live => _host.Settings.Live;

    public void Start(ClipboardMonitor? clipboard)
    {
        _battery.Start();
        _volume.Start();
        if (clipboard is not null) clipboard.CapturedItem += (isImage, text) =>
            Flash(LiveKind.Clipboard, "Copied · " + LiveActivityPolicy.CopiedLabel(text, isImage, Live.HideCopiedText), "#5E5CE6");
        _host.NowPlaying.Changed += started =>
        {
            if (!started || _host.NowPlaying.Current is not { } t) return;
            Flash(LiveKind.NowPlaying, string.IsNullOrWhiteSpace(t.Artist) ? "♪ " + t.Title : $"♪ {t.Title} – {t.Artist}", MusicHex(t));
        };
    }

    public static string MusicHex(NowPlayingTrack t) => $"#{t.Color.R:X2}{t.Color.G:X2}{t.Color.B:X2}";

    /// <summary>Shows one banner if that kind of event is allowed. Returns whether it was shown.</summary>
    public bool Flash(LiveKind kind, string text, string colorHex)
    {
        if (!LiveActivityPolicy.Allows(Live, kind)) return false;
        if (BannerSpec.TryCreate(text, BannerAnimation.Slide, 1, colorHex, BannerFinish.Solid, Math.Clamp(Live.EventSeconds, 1, 10), false) is not { } spec) return false;
        _host.FlashBanner(spec);
        return true;
    }

    private void CheckBattery()
    {
        if (!Live.Battery) { _batteryKnown = false; return; }
        var now = _host.Battery.Read();
        if (now is null) return;
        if (_batteryKnown && _lastBattery is { } before)
        {
            if (now.PluggedIn && !before.PluggedIn) Flash(LiveKind.Battery, $"Charging · {now.Percent}%", "#30D158");
            else if (!now.PluggedIn && before.PluggedIn) Flash(LiveKind.Battery, $"On battery · {now.Percent}%", "#FFD60A");
            else if (now.PluggedIn && now.Percent >= 100 && !_fullAnnounced) { _fullAnnounced = true; Flash(LiveKind.Battery, "Fully charged", "#30D158"); }
        }
        if (!now.PluggedIn || now.Percent < 95) _fullAnnounced = false;
        _lastBattery = now;
        _batteryKnown = true;
    }

    private void CheckVolume()
    {
        if (!Live.Volume) { _lastVolume = -1; _pendingVolume = -1; return; }
        var v = _host.Volume.Get();
        if (v is null) return;
        if (_lastVolume < 0) { _lastVolume = v.Percent; _lastMuted = v.Muted; return; }
        if (v.Percent != _lastVolume || v.Muted != _lastMuted)
        {
            _lastVolume = v.Percent; _lastMuted = v.Muted;
            _pendingVolume = v.Percent; _pendingMuted = v.Muted;
            _volumeChangedAt = DateTime.UtcNow;
            return;
        }
        // announce once the level has stopped moving, so dragging the slider does not restart the banner all the time
        if (_pendingVolume >= 0 && DateTime.UtcNow - _volumeChangedAt > TimeSpan.FromMilliseconds(600))
        {
            Flash(LiveKind.Volume, _pendingMuted ? "Muted" : $"Volume · {_pendingVolume}%", "#32ADE6");
            _pendingVolume = -1;
        }
    }
}
