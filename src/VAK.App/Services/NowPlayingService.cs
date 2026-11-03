using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Media.Control;

namespace VAK.App.Services;

public record NowPlayingTrack(string Title, string Artist, bool Playing, string App, bool IsBrowser, ImageSource? Art, Color Color);

/// <summary>What Windows says is playing, from the system media controls (Spotify, Apple Music, browsers, video players...).
/// It only reads the title, artist, cover and play state, and can send play/pause/next/previous.</summary>
public sealed class NowPlayingService
{
    private static readonly string[] Browsers = { "chrome", "msedge", "firefox", "opera", "brave", "vivaldi", "arc", "iexplore" };

    private readonly Dispatcher _dispatcher = Application.Current.Dispatcher;
    private readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private string _artKey = "";
    private ImageSource? _art;
    private Color _color = Colors.White;
    private int _refreshing;

    public NowPlayingTrack? Current { get; private set; }

    /// <summary>Raised on the UI thread after the track or play state changed. The flag is true when it is a different track or playback just started.</summary>
    public event Action<bool>? Changed;

    /// <summary>Returns whether browser media (videos, ads) may be shown. Set from the Live Activities settings.</summary>
    public Func<bool>? AllowBrowser { get; set; }
    public Func<bool>? Enabled { get; set; }

    public async void Start()
    {
        _debounce.Tick += async (_, _) => { _debounce.Stop(); await RefreshAsync(); };
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _manager.CurrentSessionChanged += (_, _) => Hook();
            Hook();
        }
        catch (Exception e) when (e is System.Runtime.InteropServices.COMException or InvalidOperationException or UnauthorizedAccessException) { }
    }

    private void Hook()
    {
        if (_session is not null)
        {
            _session.MediaPropertiesChanged -= OnChanged;
            _session.PlaybackInfoChanged -= OnChanged;
        }
        _session = _manager?.GetCurrentSession();
        if (_session is not null)
        {
            _session.MediaPropertiesChanged += OnChanged;
            _session.PlaybackInfoChanged += OnChanged;
        }
        Schedule();
    }

    private void OnChanged(GlobalSystemMediaTransportControlsSession s, object args) => Schedule();

    private void Schedule() => _dispatcher.BeginInvoke(() => { _debounce.Stop(); _debounce.Start(); });

    /// <summary>Re-reads the session now (also used after the Live Activities toggles change).</summary>
    public void Reevaluate() => Schedule();

    private async Task RefreshAsync()
    {
        if (Interlocked.Exchange(ref _refreshing, 1) == 1) { Schedule(); return; }
        try
        {
            var before = Current;
            Current = await ReadAsync();
            var started = Current is { Playing: true } && (before is null || !before.Playing || before.Title != Current.Title || before.Artist != Current.Artist);
            if (!Equals(before, Current)) Changed?.Invoke(started);
        }
        finally { Interlocked.Exchange(ref _refreshing, 0); }
    }

    private async Task<NowPlayingTrack?> ReadAsync()
    {
        var session = _session;
        if (session is null || Enabled?.Invoke() == false) return null;
        try
        {
            var app = session.SourceAppUserModelId ?? "";
            var browser = Browsers.Any(b => app.Contains(b, StringComparison.OrdinalIgnoreCase));
            if (browser && AllowBrowser?.Invoke() == false) return null;
            var playing = session.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            var props = await session.TryGetMediaPropertiesAsync();
            if (props is null || (string.IsNullOrWhiteSpace(props.Title) && string.IsNullOrWhiteSpace(props.Artist))) return null;
            var key = $"{props.Title}|{props.Artist}|{app}";
            if (key != _artKey)
            {
                _artKey = key;
                (_art, _color) = await LoadArtAsync(props.Thumbnail);
            }
            return new NowPlayingTrack(props.Title ?? "", props.Artist ?? "", playing, app, browser, _art, _color);
        }
        catch (Exception e) when (e is System.Runtime.InteropServices.COMException or InvalidOperationException) { return null; }
    }

    private static async Task<(ImageSource?, Color)> LoadArtAsync(Windows.Storage.Streams.IRandomAccessStreamReference? thumbnail)
    {
        if (thumbnail is null) return (null, Colors.White);
        try
        {
            using var stream = await thumbnail.OpenReadAsync();
            using var net = System.IO.WindowsRuntimeStreamExtensions.AsStreamForRead(stream);
            var memory = new MemoryStream();
            await net.CopyToAsync(memory);
            memory.Position = 0;
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 160;
            image.StreamSource = memory;
            image.EndInit();
            image.Freeze();
            return (image, Dominant(image));
        }
        catch (Exception e) when (e is IOException or NotSupportedException or System.Runtime.InteropServices.COMException or InvalidOperationException or FileFormatException)
        {
            return (null, Colors.White);
        }
    }

    /// <summary>The most colourful average of a cover, so the beat on the notch matches the album.</summary>
    public static Color Dominant(BitmapSource image)
    {
        var small = new TransformedBitmap(image, new ScaleTransform(12.0 / image.PixelWidth, 12.0 / image.PixelHeight));
        var converted = new FormatConvertedBitmap(small, PixelFormats.Bgra32, null, 0);
        var stride = converted.PixelWidth * 4;
        var pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);
        double r = 0, g = 0, b = 0, total = 0;
        for (var i = 0; i + 3 < pixels.Length; i += 4)
        {
            double pb = pixels[i], pg = pixels[i + 1], pr = pixels[i + 2];
            var max = Math.Max(pr, Math.Max(pg, pb)); var min = Math.Min(pr, Math.Min(pg, pb));
            var weight = (max - min) / 255.0 * (max / 255.0) + 0.02;
            r += pr * weight; g += pg * weight; b += pb * weight; total += weight;
        }
        if (total <= 0) return Colors.White;
        var color = Color.FromRgb((byte)(r / total), (byte)(g / total), (byte)(b / total));
        // lift dark covers so the bars stay visible on a black notch
        var peak = Math.Max(color.R, Math.Max(color.G, color.B));
        if (peak is > 0 and < 150) { var k = 190.0 / peak; color = Color.FromRgb((byte)Math.Min(255, color.R * k), (byte)Math.Min(255, color.G * k), (byte)Math.Min(255, color.B * k)); }
        return color;
    }

    public async void PlayPause() { try { if (_session is not null) await _session.TryTogglePlayPauseAsync(); } catch (Exception e) when (e is System.Runtime.InteropServices.COMException) { } }
    public async void Next() { try { if (_session is not null) await _session.TrySkipNextAsync(); } catch (Exception e) when (e is System.Runtime.InteropServices.COMException) { } }
    public async void Previous() { try { if (_session is not null) await _session.TrySkipPreviousAsync(); } catch (Exception e) when (e is System.Runtime.InteropServices.COMException) { } }
}
