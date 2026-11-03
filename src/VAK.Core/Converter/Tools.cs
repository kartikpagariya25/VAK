using System.IO.Compression;
using System.Net;
using System.Text.RegularExpressions;

namespace VAK.Core.Converter;

/// <summary>Turns HTML and Markdown into plain readable text for the PDF writer.</summary>
public static class MarkupText
{
    public static string FromHtml(string html)
    {
        var s = Regex.Replace(html, @"<(script|style)\b.*?</\1\s*>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<li\b[^>]*>", "\n- ", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"</?(br|p|div|h[1-6]|tr|ul|ol|table|section|article|header|footer)\b[^>]*>", "\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<[^>]+>", "");
        s = WebUtility.HtmlDecode(s).Replace(' ', ' ');
        s = Regex.Replace(s, @"[ \t]+", " ");
        s = Regex.Replace(s, @" ?\n ?", "\n");
        return Regex.Replace(s, @"\n{3,}", "\n\n").Trim();
    }

    public static string FromMarkdown(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n').Select(line =>
        {
            var l = Regex.Replace(line, @"^\s{0,3}#{1,6}\s+", "");
            l = Regex.Replace(l, @"!\[([^\]]*)\]\([^)]*\)", "$1");
            l = Regex.Replace(l, @"\[([^\]]+)\]\([^)]*\)", "$1");
            l = Regex.Replace(l, @"(\*\*|__)(.+?)\1", "$2");
            l = Regex.Replace(l, @"(?<![\w*])\*(?!\s)(.+?)(?<!\s)\*(?![\w*])", "$1");
            l = Regex.Replace(l, @"(?<!\w)_(?!\s)(.+?)(?<!\s)_(?!\w)", "$1");
            l = Regex.Replace(l, @"`([^`]+)`", "$1");
            return l;
        });
        return string.Join("\n", lines);
    }
}

public record IcoImage(byte[] Png, int Width, int Height);

public static class IcoWriter
{
    /// <summary>Builds a .ico file that holds PNG images (supported by Windows Vista and later). A size of 256 is stored as 0.</summary>
    public static byte[] FromPngs(IReadOnlyList<IcoImage> images)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write((ushort)0); w.Write((ushort)1); w.Write((ushort)images.Count);
        var offset = 6 + 16 * images.Count;
        foreach (var img in images)
        {
            w.Write((byte)(img.Width >= 256 ? 0 : img.Width));
            w.Write((byte)(img.Height >= 256 ? 0 : img.Height));
            w.Write((byte)0); w.Write((byte)0);
            w.Write((ushort)1); w.Write((ushort)32);
            w.Write(img.Png.Length); w.Write(offset);
            offset += img.Png.Length;
        }
        foreach (var img in images) w.Write(img.Png);
        return ms.ToArray();
    }
}

public static class ZipTool
{
    /// <summary>Zips a single file or a whole folder (the folder's contents sit at the top level of the archive).</summary>
    public static void Create(string source, string zipPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(zipPath)!);
        if (File.Exists(zipPath)) File.Delete(zipPath);
        if (Directory.Exists(source))
        {
            ZipFile.CreateFromDirectory(source, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);
            return;
        }
        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        archive.CreateEntryFromFile(source, Path.GetFileName(source), CompressionLevel.Optimal);
    }

    /// <summary>Extracts into <paramref name="destination"/>. Entries that would land outside it (zip-slip) are skipped and counted;
    /// an archive that expands past <paramref name="maxTotalBytes"/> or holds too many entries is refused.</summary>
    public static int Extract(string zipPath, string destination, long maxTotalBytes = 4L * 1024 * 1024 * 1024, int maxEntries = 20000)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        if (archive.Entries.Count > maxEntries) throw new InvalidDataException("The archive has too many entries.");
        if (archive.Entries.Sum(e => e.Length) > maxTotalBytes) throw new InvalidDataException("The archive would expand to more than the allowed size.");

        Directory.CreateDirectory(destination);
        var root = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        long written = 0;
        var skipped = 0;
        foreach (var entry in archive.Entries)
        {
            var target = Path.GetFullPath(Path.Combine(destination, entry.FullName));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) { skipped++; continue; }
            if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\')) { Directory.CreateDirectory(target); continue; }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using var input = entry.Open();
            using var output = File.Create(target);
            var buffer = new byte[81920];
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                written += read; // count what is really written: an entry can claim a small size and expand
                if (written > maxTotalBytes) throw new InvalidDataException("The archive expanded past the allowed size.");
                output.Write(buffer, 0, read);
            }
        }
        return skipped;
    }
}
