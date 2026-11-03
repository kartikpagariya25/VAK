using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VAK.Core.Converter;

namespace VAK.App.Services;

public enum ItemState { Waiting, Running, Done, Failed, Cancelled }

public sealed class ConvertItem
{
    public required string Path { get; init; }
    public required FileKind Kind { get; init; }
    public required IReadOnlyList<TargetFormat> Targets { get; init; }
    public TargetFormat? Target { get; set; }
    public ItemState State { get; set; } = ItemState.Waiting;
    public string Message { get; set; } = "";
    public string? Output { get; set; }
    public string Name => System.IO.Path.GetFileName(Path.TrimEnd('\\', '/'));
}

/// <summary>Holds the queue of files and converts them one at a time on background threads. Nothing is ever overwritten,
/// a failed file does not stop the rest, and a cancelled or failed conversion leaves no half-written output behind.</summary>
public sealed class ConverterService
{
    private readonly AppHost _host;
    private CancellationTokenSource? _cts;

    public List<ConvertItem> Items { get; } = new();
    public bool Running { get; private set; }
    public string? FfmpegPath { get; private set; }

    public event Action? Changed;

    public ConverterService(AppHost host)
    {
        _host = host;
        FindFfmpeg();
    }

    public bool FindFfmpeg()
    {
        FfmpegPath = FfmpegLocator.Find(File.Exists, Environment.GetEnvironmentVariable("PATH"), FfmpegLocator.CommonFolders());
        Changed?.Invoke();
        return FfmpegPath is not null;
    }

    public bool OfficeAvailable => LibreOfficePath() is not null || WordAvailable();

    public void Add(IEnumerable<string> paths)
    {
        foreach (var p in paths)
        {
            if (!File.Exists(p) && !Directory.Exists(p)) continue;
            if (Items.Any(i => string.Equals(i.Path, p, StringComparison.OrdinalIgnoreCase))) continue;
            var targets = FormatCatalog.TargetsFor(p);
            Items.Add(new ConvertItem { Path = p, Kind = FormatCatalog.KindOf(p), Targets = targets, Target = targets.FirstOrDefault() });
        }
        Changed?.Invoke();
    }

    public void Remove(ConvertItem item) { if (!Running) { Items.Remove(item); Changed?.Invoke(); } }
    public void Clear() { if (!Running) { Items.Clear(); Changed?.Invoke(); } }
    public void SetTarget(ConvertItem item, TargetFormat target) { item.Target = target; item.State = ItemState.Waiting; item.Message = ""; Changed?.Invoke(); }
    public void Cancel() => _cts?.Cancel();

    public async Task RunAsync()
    {
        if (Running) return;
        Running = true;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        foreach (var item in Items.Where(i => i.State is ItemState.Waiting or ItemState.Failed or ItemState.Cancelled && i.Target is not null).ToList())
        {
            if (item.Target is null) continue;
            if (token.IsCancellationRequested) { item.State = ItemState.Cancelled; item.Message = "Cancelled"; continue; }
            item.State = ItemState.Running; item.Message = ""; Changed?.Invoke();
            var target = item.Target;
            var output = OutputPlanner.Plan(item.Path, target, p => File.Exists(p) || Directory.Exists(p));
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(output)!);
                await Task.Run(() => Convert(item, target, output, token), token).ConfigureAwait(true);
                item.State = ItemState.Done; item.Output = output; if (item.Message.Length == 0) item.Message = "Done";
            }
            catch (OperationCanceledException) { Cleanup(output); item.State = ItemState.Cancelled; item.Message = "Cancelled"; }
            catch (Exception e)
            {
                Cleanup(output);
                item.State = ItemState.Failed; item.Message = Short(e.Message);
            }
            Changed?.Invoke();
        }
        Running = false;
        Changed?.Invoke();
    }

    private static string Short(string message)
    {
        var line = message.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0) ?? "Failed";
        return line.Length > 140 ? line[..140] + "…" : line;
    }

    private static void Cleanup(string output)
    {
        try { if (File.Exists(output)) File.Delete(output); else if (Directory.Exists(output)) Directory.Delete(output, true); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    private void Convert(ConvertItem item, TargetFormat target, string output, CancellationToken token)
    {
        switch (target.Engine)
        {
            case Engine.Zip: ZipTool.Create(item.Path, output); break;
            case Engine.Unzip:
                var skipped = ZipTool.Extract(item.Path, output);
                if (skipped > 0) item.Message = $"{skipped} unsafe entries skipped";
                break;
            case Engine.Pdf: ToPdf(item, output); break;
            case Engine.Wic: StaRunner.Run(() => ConvertImage(item.Path, target, output, _host.Settings.Converter)); break;
            case Engine.Ffmpeg: RunFfmpeg(item.Path, output, target.Id, token); break;
            case Engine.Office: OfficeToPdf(item.Path, output, token); break;
        }
    }

    // ---- images ------------------------------------------------------------------------------

    private static BitmapSource Load(string path)
    {
        var decoder = BitmapDecoder.Create(new Uri(path), BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        frame.Freeze();
        return frame;
    }

    private static BitmapSource Fit(BitmapSource source, int maxSide)
    {
        if (maxSide <= 0 || Math.Max(source.PixelWidth, source.PixelHeight) <= maxSide) return source;
        var scale = (double)maxSide / Math.Max(source.PixelWidth, source.PixelHeight);
        var scaled = new TransformedBitmap(source, new ScaleTransform(scale, scale));
        scaled.Freeze();
        return scaled;
    }

    /// <summary>Transparent areas become white (for formats without alpha).</summary>
    private static BitmapSource OnWhite(BitmapSource source)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, source.PixelWidth, source.PixelHeight));
            dc.DrawImage(source, new Rect(0, 0, source.PixelWidth, source.PixelHeight));
        }
        var target = new RenderTargetBitmap(source.PixelWidth, source.PixelHeight, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        var rgb = new FormatConvertedBitmap(target, PixelFormats.Bgr24, null, 0);
        rgb.Freeze();
        return rgb;
    }

    private static byte[] Encode(BitmapEncoder encoder, BitmapSource source)
    {
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }

    private static void ConvertImage(string input, TargetFormat target, string output, Core.Settings.ConverterSettings options)
    {
        var image = Fit(Load(input), options.MaxSide);
        byte[] bytes;
        switch (target.Id)
        {
            case "png": bytes = Encode(new PngBitmapEncoder(), image); break;
            case "jpg": bytes = Encode(new JpegBitmapEncoder { QualityLevel = Math.Clamp(options.Quality, 10, 100) }, OnWhite(image)); break;
            case "bmp": bytes = Encode(new BmpBitmapEncoder(), OnWhite(image)); break;
            case "gif": bytes = Encode(new GifBitmapEncoder(), image); break;
            case "tiff": bytes = Encode(new TiffBitmapEncoder { Compression = TiffCompressOption.Lzw }, image); break;
            case "ico": bytes = IcoWriter.FromPngs(IconSizes(image)); break;
            default: throw new InvalidOperationException($"Cannot make {target.Label} here.");
        }
        File.WriteAllBytes(output, bytes);
    }

    private static List<IcoImage> IconSizes(BitmapSource source)
    {
        var list = new List<IcoImage>();
        foreach (var size in new[] { 16, 32, 48, 64, 128, 256 })
        {
            var scale = Math.Min((double)size / source.PixelWidth, (double)size / source.PixelHeight);
            var w = source.PixelWidth * scale; var h = source.PixelHeight * scale;
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen()) dc.DrawImage(source, new Rect((size - w) / 2, (size - h) / 2, w, h));
            var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(visual);
            bmp.Freeze();
            list.Add(new IcoImage(Encode(new PngBitmapEncoder(), bmp), size, size));
        }
        return list;
    }

    // ---- PDF ---------------------------------------------------------------------------------

    private void ToPdf(ConvertItem item, string output)
    {
        if (item.Kind == FileKind.Image)
        {
            byte[] pdf = null!;
            StaRunner.Run(() =>
            {
                var image = Fit(Load(item.Path), 3000);
                var jpeg = Encode(new JpegBitmapEncoder { QualityLevel = 90 }, OnWhite(image));
                pdf = PdfBuilder.FromJpegs(new[] { new JpegPage(jpeg, image.PixelWidth, image.PixelHeight) });
            });
            File.WriteAllBytes(output, pdf);
            return;
        }
        var info = new FileInfo(item.Path);
        if (info.Length > 20 * 1024 * 1024) throw new InvalidOperationException("That text file is too large to convert (limit 20 MB).");
        var text = File.ReadAllText(item.Path, Encoding.UTF8);
        var ext = info.Extension.TrimStart('.').ToLowerInvariant();
        text = ext is "html" or "htm" ? MarkupText.FromHtml(text) : ext is "md" or "markdown" ? MarkupText.FromMarkdown(text) : text;
        File.WriteAllBytes(output, PdfBuilder.FromText(text));
    }

    // ---- ffmpeg ------------------------------------------------------------------------------

    private void RunFfmpeg(string input, string output, string targetId, CancellationToken token)
    {
        if (FfmpegPath is null) throw new InvalidOperationException("ffmpeg was not found. Install it, then press Check again.");
        var psi = new ProcessStartInfo(FfmpegPath)
        {
            RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true,
        };
        foreach (var a in FfmpegArgs.Build(input, output, targetId)) psi.ArgumentList.Add(a);
        using var process = Process.Start(psi) ?? throw new InvalidOperationException("ffmpeg could not be started.");
        var errors = new StringBuilder();
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null && errors.Length < 4000) errors.AppendLine(e.Data); };
        process.BeginErrorReadLine();
        process.BeginOutputReadLine();
        using (token.Register(() => { try { process.Kill(true); } catch (InvalidOperationException) { } }))
            process.WaitForExit();
        token.ThrowIfCancellationRequested();
        if (process.ExitCode != 0 || !File.Exists(output))
            throw new InvalidOperationException(errors.Length > 0 ? errors.ToString().Trim().Split('\n').Last().Trim() : $"ffmpeg stopped with code {process.ExitCode}.");
    }

    // ---- Office to PDF -----------------------------------------------------------------------

    private static string? LibreOfficePath()
    {
        foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) })
        {
            var p = System.IO.Path.Combine(root, "LibreOffice", "program", "soffice.exe");
            if (File.Exists(p)) return p;
        }
        return null;
    }

    private static bool WordAvailable() => Type.GetTypeFromProgID("Word.Application") is not null;

    private void OfficeToPdf(string input, string output, CancellationToken token)
    {
        if (LibreOfficePath() is { } soffice)
        {
            var temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "vak-office-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try
            {
                var psi = new ProcessStartInfo(soffice) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
                foreach (var a in new[] { "--headless", "--convert-to", "pdf", "--outdir", temp, input }) psi.ArgumentList.Add(a);
                using var p = Process.Start(psi) ?? throw new InvalidOperationException("LibreOffice could not be started.");
                using (token.Register(() => { try { p.Kill(true); } catch (InvalidOperationException) { } })) p.WaitForExit(120_000);
                token.ThrowIfCancellationRequested();
                var produced = Directory.GetFiles(temp, "*.pdf").FirstOrDefault() ?? throw new InvalidOperationException("LibreOffice did not produce a PDF.");
                File.Move(produced, output);
            }
            finally { try { Directory.Delete(temp, true); } catch (IOException) { } }
            return;
        }
        if (WordAvailable() && FormatCatalog.KindOf(input) == FileKind.Office &&
            System.IO.Path.GetExtension(input).ToLowerInvariant() is ".doc" or ".docx" or ".rtf" or ".odt")
        {
            StaRunner.Run(() =>
            {
                dynamic word = Activator.CreateInstance(Type.GetTypeFromProgID("Word.Application")!)!;
                try
                {
                    word.Visible = false; word.DisplayAlerts = 0;
                    dynamic doc = word.Documents.Open(input, ReadOnly: true, Visible: false);
                    try { doc.ExportAsFixedFormat(output, 17); } // 17 = PDF
                    finally { doc.Close(false); }
                }
                finally { word.Quit(false); }
            });
            return;
        }
        throw new InvalidOperationException("Converting Office files needs LibreOffice or Microsoft Word installed.");
    }
}

/// <summary>Runs work on a short-lived STA thread (WPF imaging and COM automation want one) and rethrows its exception.</summary>
internal static class StaRunner
{
    public static void Run(Action work)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { work(); } catch (Exception e) { failure = e; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
