using System.Globalization;
using System.Text;

namespace VAK.Core.Converter;

public record JpegPage(byte[] Data, int Width, int Height);

/// <summary>Writes simple PDF 1.4 files with no outside library: plain text (Helvetica, A4, wrapped and paginated) and
/// one-image-per-page documents from JPEG data. Text outside Latin-1 becomes "?".</summary>
public static class PdfBuilder
{
    private const int PageWidth = 595, PageHeight = 842, Margin = 56;
    private const int FontSize = 11, Leading = 14;
    private const int MaxChars = 86;                                                // roughly (595 - 2*56) / (11 * 0.5)
    private const int LinesPerPage = (PageHeight - 2 * Margin) / Leading;

    public static byte[] FromText(string text)
    {
        var lines = Wrap(text);
        var pages = new List<List<string>>();
        for (var i = 0; i < lines.Count; i += LinesPerPage) pages.Add(lines.GetRange(i, Math.Min(LinesPerPage, lines.Count - i)));
        if (pages.Count == 0) pages.Add(new List<string>());

        var doc = new Doc();
        var catalog = doc.Reserve(); var pagesRoot = doc.Reserve(); var font = doc.Reserve();
        var pageIds = new List<int>();
        foreach (var page in pages)
        {
            var content = new StringBuilder();
            content.Append($"BT /F1 {FontSize} Tf {Margin} {PageHeight - Margin - FontSize} Td {Leading} TL\n");
            foreach (var line in page) content.Append('(').Append(Escape(line)).Append(") Tj T*\n");
            content.Append("ET\n");
            var contentId = doc.Add(Stream("", Encoding.Latin1.GetBytes(content.ToString())));
            pageIds.Add(doc.Add(Latin1($"<< /Type /Page /Parent {pagesRoot} 0 R /MediaBox [0 0 {PageWidth} {PageHeight}] /Resources << /Font << /F1 {font} 0 R >> >> /Contents {contentId} 0 R >>")));
        }
        doc.Set(catalog, Latin1($"<< /Type /Catalog /Pages {pagesRoot} 0 R >>"));
        doc.Set(pagesRoot, Latin1($"<< /Type /Pages /Kids [{string.Join(" ", pageIds.Select(p => p + " 0 R"))}] /Count {pageIds.Count} >>"));
        doc.Set(font, Latin1("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"));
        return doc.Write(catalog);
    }

    public static byte[] FromJpegs(IEnumerable<JpegPage> images)
    {
        var doc = new Doc();
        var catalog = doc.Reserve(); var pagesRoot = doc.Reserve();
        var pageIds = new List<int>();
        foreach (var img in images)
        {
            var imageId = doc.Add(Stream($"/Type /XObject /Subtype /Image /Width {img.Width} /Height {img.Height} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode", img.Data));
            // scale to fit inside the margins, keep the aspect ratio, centre it
            var maxW = PageWidth - 2.0 * Margin / 2; var maxH = PageHeight - 2.0 * Margin / 2;
            var scale = Math.Min(maxW / Math.Max(img.Width, 1), maxH / Math.Max(img.Height, 1));
            var w = img.Width * scale; var h = img.Height * scale;
            var x = (PageWidth - w) / 2; var y = (PageHeight - h) / 2;
            var draw = string.Create(CultureInfo.InvariantCulture, $"q {w:0.##} 0 0 {h:0.##} {x:0.##} {y:0.##} cm /Im0 Do Q\n");
            var contentId = doc.Add(Stream("", Encoding.Latin1.GetBytes(draw)));
            pageIds.Add(doc.Add(Latin1($"<< /Type /Page /Parent {pagesRoot} 0 R /MediaBox [0 0 {PageWidth} {PageHeight}] /Resources << /XObject << /Im0 {imageId} 0 R >> >> /Contents {contentId} 0 R >>")));
        }
        doc.Set(catalog, Latin1($"<< /Type /Catalog /Pages {pagesRoot} 0 R >>"));
        doc.Set(pagesRoot, Latin1($"<< /Type /Pages /Kids [{string.Join(" ", pageIds.Select(p => p + " 0 R"))}] /Count {pageIds.Count} >>"));
        return doc.Write(catalog);
    }

    // ---- text layout -------------------------------------------------------------------------

    private static List<string> Wrap(string text)
    {
        var result = new List<string>();
        foreach (var raw in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var line = raw.Replace("\t", "    ");
            if (line.Length == 0) { result.Add(""); continue; }
            while (line.Length > MaxChars)
            {
                var cut = line.LastIndexOf(' ', MaxChars);
                if (cut <= 0) cut = MaxChars; // one long word: split it
                result.Add(line[..cut].TrimEnd());
                line = line[cut..].TrimStart();
            }
            result.Add(line);
        }
        return result;
    }

    private static string Escape(string line)
    {
        var sb = new StringBuilder(line.Length);
        foreach (var c in line)
        {
            if (c is '(' or ')' or '\\') sb.Append('\\').Append(c);
            else if ((c >= 32 && c <= 126) || (c >= 160 && c <= 255)) sb.Append(c);
            else sb.Append('?');
        }
        return sb.ToString();
    }

    // ---- file structure ----------------------------------------------------------------------

    private static byte[] Latin1(string s) => Encoding.Latin1.GetBytes(s);

    private static byte[] Stream(string dictionaryEntries, byte[] data)
    {
        using var ms = new MemoryStream();
        var head = Latin1($"<< {dictionaryEntries} /Length {data.Length} >>\nstream\n");
        ms.Write(head); ms.Write(data); ms.Write(Latin1("\nendstream"));
        return ms.ToArray();
    }

    /// <summary>Collects numbered objects, then writes them with a cross-reference table.</summary>
    private sealed class Doc
    {
        private readonly List<byte[]?> _objects = new();

        public int Reserve() { _objects.Add(null); return _objects.Count; }
        public int Add(byte[] body) { _objects.Add(body); return _objects.Count; }
        public void Set(int id, byte[] body) => _objects[id - 1] = body;

        public byte[] Write(int rootId)
        {
            using var ms = new MemoryStream();
            ms.Write(Latin1("%PDF-1.4\n"));
            var offsets = new List<long>();
            for (var i = 0; i < _objects.Count; i++)
            {
                offsets.Add(ms.Position);
                ms.Write(Latin1($"{i + 1} 0 obj\n")); ms.Write(_objects[i] ?? Latin1("null")); ms.Write(Latin1("\nendobj\n"));
            }
            var xref = ms.Position;
            var sb = new StringBuilder($"xref\n0 {_objects.Count + 1}\n0000000000 65535 f \n");
            foreach (var o in offsets) sb.Append(o.ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
            sb.Append($"trailer\n<< /Size {_objects.Count + 1} /Root {rootId} 0 R >>\nstartxref\n{xref}\n%%EOF\n");
            ms.Write(Latin1(sb.ToString()));
            return ms.ToArray();
        }
    }
}
