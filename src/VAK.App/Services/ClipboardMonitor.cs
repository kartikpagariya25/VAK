using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VAK.Core.Clipboard;

namespace VAK.App.Services;

/// <summary>Listens for clipboard changes and records them in the history. Copies made by VAK itself and items that
/// password managers mark as private (ExcludeClipboardContentFromMonitorProcessing) are never recorded.</summary>
public sealed class ClipboardMonitor : IDisposable
{
    private const int WM_CLIPBOARDUPDATE = 0x031D;
    private static readonly IntPtr HwndMessage = new(-3);

    private readonly ClipboardHistory _history;
    private readonly string _imageDir;
    private readonly HwndSource _source;
    private readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private DateTime _ignoreUntil = DateTime.MinValue;

    /// <summary>Raised after something new was recorded.</summary>
    public event Action? Captured;

    public ClipboardMonitor(ClipboardHistory history, string dataDir)
    {
        _history = history;
        _imageDir = Path.Combine(dataDir, "clipboard");
        _history.ImageDropped += file => { try { File.Delete(ImagePath(file)); } catch (IOException) { } };

        _source = new HwndSource(new HwndSourceParameters("VAKClipboard") { ParentWindow = HwndMessage, Width = 0, Height = 0 });
        _source.AddHook(WndProc);
        AddClipboardFormatListener(_source.Handle);
        _debounce.Tick += (_, _) => { _debounce.Stop(); Capture(); };
    }

    public string ImagePath(string file) => Path.Combine(_imageDir, file);

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_CLIPBOARDUPDATE) { _debounce.Stop(); _debounce.Start(); }
        return IntPtr.Zero;
    }

    private void Capture()
    {
        if (DateTime.UtcNow < _ignoreUntil) return;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                if (Clipboard.ContainsData("ExcludeClipboardContentFromMonitorProcessing")) return;
                if (Clipboard.ContainsText())
                {
                    if (_history.AddText(Clipboard.GetText()) is not null) Captured?.Invoke();
                }
                else if (Clipboard.ContainsImage() && Clipboard.GetImage() is { } image) SaveImage(image);
                return;
            }
            catch (Exception e) when (e is COMException or ExternalException) { Thread.Sleep(30); } // another app holds the clipboard
        }
    }

    private void SaveImage(BitmapSource image)
    {
        if (image.PixelWidth > 8000 || image.PixelHeight > 8000) return;
        try
        {
            Directory.CreateDirectory(_imageDir);
            var file = Guid.NewGuid().ToString("N") + ".png";
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using (var stream = File.Create(ImagePath(file))) encoder.Save(stream);
            _history.AddImage(file, image.PixelWidth, image.PixelHeight);
            Captured?.Invoke();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Puts an entry back on the clipboard without recording that copy as new history.</summary>
    public void CopyBack(ClipEntry entry)
    {
        _ignoreUntil = DateTime.UtcNow.AddMilliseconds(600);
        try
        {
            if (entry.Kind == ClipKind.Image)
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(ImagePath(entry.ImageFile));
                bmp.EndInit();
                Clipboard.SetImage(bmp);
            }
            else Clipboard.SetText(entry.Text);
        }
        catch (Exception e) when (e is COMException or ExternalException or IOException or NotSupportedException) { }
    }

    public void Dispose()
    {
        RemoveClipboardFormatListener(_source.Handle);
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }

    [DllImport("user32.dll", SetLastError = true)] private static extern bool AddClipboardFormatListener(IntPtr hwnd);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
}
