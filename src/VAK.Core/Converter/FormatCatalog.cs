namespace VAK.Core.Converter;

public enum FileKind { Image, Audio, Video, Text, Office, Archive, Folder, Other }

/// <summary>Which part of VAK (or which outside tool) does the work.</summary>
public enum Engine { Wic, Pdf, Ffmpeg, Office, Zip, Unzip }

public record TargetFormat(string Id, string Label, string Extension, Engine Engine);

public static class FormatCatalog
{
    private static readonly Dictionary<string, FileKind> Kinds = Build();

    private static Dictionary<string, FileKind> Build()
    {
        var d = new Dictionary<string, FileKind>(StringComparer.OrdinalIgnoreCase);
        void Add(FileKind kind, params string[] exts) { foreach (var e in exts) d[e] = kind; }
        Add(FileKind.Image, "png", "jpg", "jpeg", "bmp", "gif", "tif", "tiff", "webp", "ico");
        Add(FileKind.Audio, "mp3", "wav", "flac", "ogg", "m4a", "aac", "wma", "opus");
        Add(FileKind.Video, "mp4", "mkv", "mov", "avi", "webm", "wmv", "m4v");
        Add(FileKind.Text, "txt", "md", "markdown", "html", "htm");
        Add(FileKind.Office, "doc", "docx", "xls", "xlsx", "ppt", "pptx", "odt", "ods", "odp", "rtf");
        Add(FileKind.Archive, "zip");
        return d;
    }

    public static FileKind KindOf(string path)
    {
        if (Directory.Exists(path)) return FileKind.Folder;
        var ext = Path.GetExtension(path).TrimStart('.');
        return Kinds.TryGetValue(ext, out var kind) ? kind : FileKind.Other;
    }

    private static readonly TargetFormat Zip = new("zip", "ZIP", "zip", Engine.Zip);

    /// <summary>The formats a file can be converted to. A file is never offered its own format.</summary>
    public static IReadOnlyList<TargetFormat> TargetsFor(string path)
    {
        var kind = KindOf(path);
        var own = Normalize(Path.GetExtension(path).TrimStart('.'));
        var list = new List<TargetFormat>();
        switch (kind)
        {
            case FileKind.Image:
                list.AddRange(new TargetFormat[]
                {
                    new("png", "PNG", "png", Engine.Wic), new("jpg", "JPG", "jpg", Engine.Wic), new("webp", "WEBP", "webp", Engine.Ffmpeg),
                    new("bmp", "BMP", "bmp", Engine.Wic), new("gif", "GIF", "gif", Engine.Wic), new("tiff", "TIFF", "tiff", Engine.Wic),
                    new("ico", "ICO", "ico", Engine.Wic), new("pdf", "PDF", "pdf", Engine.Pdf),
                });
                break;
            case FileKind.Audio:
                list.AddRange(new TargetFormat[]
                {
                    new("mp3", "MP3", "mp3", Engine.Ffmpeg), new("wav", "WAV", "wav", Engine.Ffmpeg), new("flac", "FLAC", "flac", Engine.Ffmpeg),
                    new("ogg", "OGG", "ogg", Engine.Ffmpeg), new("m4a", "M4A", "m4a", Engine.Ffmpeg),
                });
                break;
            case FileKind.Video:
                list.AddRange(new TargetFormat[]
                {
                    new("mp4", "MP4", "mp4", Engine.Ffmpeg), new("webm", "WebM", "webm", Engine.Ffmpeg), new("mkv", "MKV", "mkv", Engine.Ffmpeg),
                    new("gif", "GIF", "gif", Engine.Ffmpeg), new("mp3", "MP3 (audio only)", "mp3", Engine.Ffmpeg),
                });
                break;
            case FileKind.Text:
                list.Add(new TargetFormat("pdf", "PDF", "pdf", Engine.Pdf));
                break;
            case FileKind.Office:
                list.Add(new TargetFormat("pdf", "PDF", "pdf", Engine.Office));
                break;
            case FileKind.Archive:
                return new[] { new TargetFormat("unzip", "Extract", "", Engine.Unzip) };
        }
        var result = list.Where(t => Normalize(t.Id) != own).ToList();
        result.Add(Zip);
        return result;
    }

    private static string Normalize(string ext)
    {
        var e = ext.ToLowerInvariant();
        return e switch { "jpeg" => "jpg", "tif" => "tiff", "htm" => "html", _ => e };
    }
}

public static class OutputPlanner
{
    public const string FolderName = "VAK Converted";

    /// <summary>Where the result goes: a "VAK Converted" folder beside the source, never overwriting (names are numbered).
    /// <paramref name="exists"/> tells whether a path is already taken.</summary>
    public static string Plan(string source, TargetFormat target, Func<string, bool> exists)
    {
        var trimmed = source.TrimEnd('\\', '/');
        var parent = Path.GetDirectoryName(trimmed) ?? "";
        var outDir = Path.Combine(parent, FolderName);
        var isFolder = Directory.Exists(source);
        var stem = isFolder ? Path.GetFileName(trimmed) : Path.GetFileNameWithoutExtension(trimmed);
        var ext = target.Engine == Engine.Unzip ? "" : "." + target.Extension;

        var candidate = Path.Combine(outDir, stem + ext);
        for (var n = 2; exists(candidate); n++) candidate = Path.Combine(outDir, $"{stem} ({n}){ext}");
        return candidate;
    }
}

public static class FfmpegLocator
{
    /// <summary>Looks for ffmpeg.exe in each PATH folder, then in the extra folders. Returns null when it is not installed.</summary>
    public static string? Find(Func<string, bool> exists, string? pathVariable, IEnumerable<string> extraDirectories)
    {
        var dirs = (pathVariable ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Concat(extraDirectories);
        foreach (var dir in dirs)
        {
            var candidate = Path.Combine(dir.Trim('"'), "ffmpeg.exe");
            if (exists(candidate)) return candidate;
        }
        return null;
    }

    /// <summary>Folders where people commonly unpack or install ffmpeg.</summary>
    public static IEnumerable<string> CommonFolders()
    {
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        yield return Path.Combine(pf, "ffmpeg", "bin");
        yield return @"C:\ffmpeg\bin";
        yield return Path.Combine(home, "scoop", "shims");
        yield return Path.Combine(local, "Microsoft", "WinGet", "Links");
        yield return @"C:\ProgramData\chocolatey\bin";
    }
}

public static class FfmpegArgs
{
    /// <summary>The ffmpeg arguments for one conversion. Each path is its own argument, so spaces and quotes in file names are safe.</summary>
    public static List<string> Build(string input, string output, string target)
    {
        var args = new List<string> { "-y", "-hide_banner", "-loglevel", "error", "-i", input };
        switch (target.ToLowerInvariant())
        {
            case "mp3": args.AddRange(new[] { "-vn", "-codec:a", "libmp3lame", "-q:a", "2" }); break;
            case "wav": args.AddRange(new[] { "-vn", "-codec:a", "pcm_s16le" }); break;
            case "flac": args.AddRange(new[] { "-vn", "-codec:a", "flac" }); break;
            case "ogg": args.AddRange(new[] { "-vn", "-codec:a", "libvorbis", "-q:a", "5" }); break;
            case "m4a": args.AddRange(new[] { "-vn", "-codec:a", "aac", "-b:a", "192k" }); break;
            case "mp4": args.AddRange(new[] { "-c:v", "libx264", "-preset", "medium", "-crf", "23", "-c:a", "aac", "-b:a", "160k", "-movflags", "+faststart" }); break;
            case "webm": args.AddRange(new[] { "-c:v", "libvpx-vp9", "-crf", "32", "-b:v", "0", "-c:a", "libopus" }); break;
            case "mkv": args.AddRange(new[] { "-c:v", "libx264", "-crf", "23", "-c:a", "aac" }); break;
            case "gif": args.AddRange(new[] { "-vf", "fps=12,scale=480:-1:flags=lanczos", "-loop", "0" }); break;
            case "webp": args.AddRange(new[] { "-quality", "90" }); break;
            default: throw new ArgumentException($"ffmpeg cannot make '{target}'.", nameof(target));
        }
        args.Add(output);
        return args;
    }
}
