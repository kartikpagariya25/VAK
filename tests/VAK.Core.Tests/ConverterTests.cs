using System.IO.Compression;
using System.Text;
using VAK.Core.Converter;
using Xunit;

namespace VAK.Core.Tests;

public class ConverterTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vak-conv-" + Guid.NewGuid().ToString("N"));
    public ConverterTests() => Directory.CreateDirectory(_dir);
    public void Dispose() => Directory.Delete(_dir, true);

    // ---- what a file can be converted to
    [Theory]
    [InlineData(@"C:\x\photo.PNG", FileKind.Image)]
    [InlineData(@"C:\x\photo.webp", FileKind.Image)]
    [InlineData(@"C:\x\song.mp3", FileKind.Audio)]
    [InlineData(@"C:\x\clip.MP4", FileKind.Video)]
    [InlineData(@"C:\x\notes.md", FileKind.Text)]
    [InlineData(@"C:\x\page.html", FileKind.Text)]
    [InlineData(@"C:\x\report.docx", FileKind.Office)]
    [InlineData(@"C:\x\pack.zip", FileKind.Archive)]
    [InlineData(@"C:\x\mystery.xyz", FileKind.Other)]
    public void KindOf_ByExtension(string path, FileKind expected) => Assert.Equal(expected, FormatCatalog.KindOf(path));

    [Fact]
    public void KindOf_Folder_IsFolder() => Assert.Equal(FileKind.Folder, FormatCatalog.KindOf(_dir));

    [Fact]
    public void Image_OffersImageFormatsAndPdf_ButNotItsOwnFormat()
    {
        var ids = FormatCatalog.TargetsFor(@"C:\x\a.png").Select(t => t.Id).ToList();
        Assert.Contains("jpg", ids); Assert.Contains("webp", ids); Assert.Contains("ico", ids); Assert.Contains("pdf", ids); Assert.Contains("zip", ids);
        Assert.DoesNotContain("png", ids);
    }

    [Fact]
    public void Jpeg_And_Jpg_AreTheSameFormat() =>
        Assert.DoesNotContain("jpg", FormatCatalog.TargetsFor(@"C:\x\a.jpeg").Select(t => t.Id));

    [Fact]
    public void Video_CanBecomeGifOrMp3_AndNeedsFfmpeg()
    {
        var targets = FormatCatalog.TargetsFor(@"C:\x\a.mkv");
        Assert.All(targets.Where(t => t.Id != "zip"), t => Assert.Equal(Engine.Ffmpeg, t.Engine));
        Assert.Contains(targets, t => t.Id == "gif");
        Assert.Contains(targets, t => t.Id == "mp3");
    }

    [Fact]
    public void Text_BecomesPdf_Office_BecomesPdf_Zip_ExtractsOnly()
    {
        Assert.Equal(Engine.Pdf, FormatCatalog.TargetsFor(@"C:\x\a.md").First(t => t.Id == "pdf").Engine);
        Assert.Equal(Engine.Office, FormatCatalog.TargetsFor(@"C:\x\a.docx").First(t => t.Id == "pdf").Engine);
        var zipTargets = FormatCatalog.TargetsFor(@"C:\x\a.zip").Select(t => t.Id).ToList();
        Assert.Equal(new[] { "unzip" }, zipTargets);
    }

    [Fact]
    public void Folder_CanOnlyBeZipped() =>
        Assert.Equal(new[] { "zip" }, FormatCatalog.TargetsFor(_dir).Select(t => t.Id));

    [Fact]
    public void UnknownFile_CanStillBeZipped() =>
        Assert.Equal(new[] { "zip" }, FormatCatalog.TargetsFor(@"C:\x\a.xyz").Select(t => t.Id));

    // ---- where the output goes
    [Fact]
    public void Output_GoesToVakConvertedNextToTheSource()
    {
        var src = Path.Combine(_dir, "pic.png");
        var target = FormatCatalog.TargetsFor(src).First(t => t.Id == "jpg");
        Assert.Equal(Path.Combine(_dir, "VAK Converted", "pic.jpg"), OutputPlanner.Plan(src, target, _ => false));
    }

    [Fact]
    public void Output_NeverOverwrites_ItNumbers()
    {
        var src = Path.Combine(_dir, "pic.png");
        var target = FormatCatalog.TargetsFor(src).First(t => t.Id == "jpg");
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Path.Combine(_dir, "VAK Converted", "pic.jpg"), Path.Combine(_dir, "VAK Converted", "pic (2).jpg") };
        Assert.Equal(Path.Combine(_dir, "VAK Converted", "pic (3).jpg"), OutputPlanner.Plan(src, target, taken.Contains));
    }

    [Fact]
    public void Output_ForAFolder_SitsBesideIt_AndUnzipUsesAFolderName()
    {
        var folder = Path.Combine(_dir, "photos");
        Directory.CreateDirectory(folder);
        Assert.Equal(Path.Combine(_dir, "VAK Converted", "photos.zip"), OutputPlanner.Plan(folder, FormatCatalog.TargetsFor(folder).Single(), _ => false));
        var zip = Path.Combine(_dir, "pack.zip");
        Assert.Equal(Path.Combine(_dir, "VAK Converted", "pack"), OutputPlanner.Plan(zip, FormatCatalog.TargetsFor(zip).Single(), _ => false));
    }

    // ---- ffmpeg
    [Fact]
    public void Locator_FindsFfmpegOnPath_ThenExtraFolders_ThenNull()
    {
        Assert.Equal(@"C:\tools\ffmpeg.exe", FfmpegLocator.Find(p => p == @"C:\tools\ffmpeg.exe", @"C:\Windows;C:\tools", Array.Empty<string>()));
        Assert.Equal(@"D:\extra\ffmpeg.exe", FfmpegLocator.Find(p => p == @"D:\extra\ffmpeg.exe", @"C:\Windows", new[] { @"D:\extra" }));
        Assert.Null(FfmpegLocator.Find(_ => false, @"C:\Windows", new[] { @"D:\extra" }));
        Assert.Null(FfmpegLocator.Find(_ => true, null, Array.Empty<string>()));
    }

    [Theory]
    [InlineData("mp3", "-vn", "libmp3lame")]
    [InlineData("flac", "-vn", "flac")]
    [InlineData("ogg", "-vn", "libvorbis")]
    [InlineData("m4a", "-vn", "aac")]
    [InlineData("mp4", "-c:v", "libx264")]
    [InlineData("webm", "-c:v", "libvpx-vp9")]
    [InlineData("gif", "-vf", "fps=12")]
    public void FfmpegArgs_SelectTheRightCodecs(string target, string flag, string value)
    {
        var args = FfmpegArgs.Build(@"C:\in file.mkv", @"C:\out file." + target, target);
        Assert.Equal("-y", args[0]);
        Assert.Contains(flag, args);
        Assert.Contains(args, a => a.Contains(value));
        Assert.Equal(@"C:\in file.mkv", args[args.IndexOf("-i") + 1]);   // paths are separate arguments, never glued into one string
        Assert.Equal(@"C:\out file." + target, args[^1]);
    }

    [Fact]
    public void FfmpegArgs_Webp_FromAnImage() =>
        Assert.Contains("-quality", FfmpegArgs.Build(@"C:\a.png", @"C:\a.webp", "webp"));

    [Fact]
    public void FfmpegArgs_UnknownTarget_Throws() =>
        Assert.Throws<ArgumentException>(() => FfmpegArgs.Build("a", "b", "doc"));

    // ---- PDF
    [Fact]
    public void Pdf_FromText_IsAWellFormedDocument()
    {
        var pdf = PdfBuilder.FromText("Hello (world) \\ backslash\nsecond line");
        var text = Encoding.Latin1.GetString(pdf);
        Assert.StartsWith("%PDF-1.4", text);
        Assert.EndsWith("%%EOF\n", text);
        Assert.Contains("Hello \\(world\\) \\\\ backslash", text); // parentheses and backslashes are escaped
        Assert.Contains("/Type /Catalog", text);
        // the xref table must point at every object
        var xref = int.Parse(text[(text.LastIndexOf("startxref", StringComparison.Ordinal) + 10)..].Split('\n')[0]);
        Assert.StartsWith("xref", text[xref..]);
    }

    [Fact]
    public void Pdf_LongText_FlowsOntoSeveralPages()
    {
        var lines = string.Join("\n", Enumerable.Range(1, 400).Select(i => "line " + i));
        var text = Encoding.Latin1.GetString(PdfBuilder.FromText(lines));
        var pages = System.Text.RegularExpressions.Regex.Matches(text, "/Type /Page\\b").Count;
        Assert.True(pages >= 7, $"expected several pages, got {pages}");
    }

    [Fact]
    public void Pdf_VeryLongLine_IsWrapped()
    {
        var text = Encoding.Latin1.GetString(PdfBuilder.FromText(new string('w', 600)));
        Assert.True(System.Text.RegularExpressions.Regex.Matches(text, @"\) Tj").Count >= 3);
    }

    [Fact]
    public void Pdf_NonLatinCharacters_BecomeQuestionMarks_NotBrokenBytes()
    {
        var bytes = PdfBuilder.FromText("price: \u20AC5 \u65E5\u672C");
        Assert.Contains("?", Encoding.Latin1.GetString(bytes));
    }

    [Fact]
    public void Pdf_FromJpegs_OnePagePerImage_ScaledToFitA4()
    {
        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
        var text = Encoding.Latin1.GetString(PdfBuilder.FromJpegs(new[] { new JpegPage(jpeg, 4000, 3000), new JpegPage(jpeg, 100, 100) }));
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(text, "/Type /Page\\b").Count);
        Assert.Contains("/Filter /DCTDecode", text);
        Assert.Contains("/MediaBox [0 0 595 842]", text);
    }

    [Fact]
    public void Pdf_NoPages_StillAValidDocument() =>
        Assert.StartsWith("%PDF-1.4", Encoding.Latin1.GetString(PdfBuilder.FromText("")));

    // ---- text preparation
    [Fact]
    public void Html_IsReducedToReadableText()
    {
        var text = MarkupText.FromHtml("<html><head><style>p{}</style><script>alert(1)</script></head><body><h1>Title</h1><p>Hello&nbsp;&amp; <b>bye</b></p><ul><li>one</li><li>two</li></ul></body></html>");
        Assert.DoesNotContain("alert", text);
        Assert.DoesNotContain("p{}", text);
        Assert.Contains("Title", text);
        Assert.Contains("Hello & bye", text);
        Assert.Contains("one", text);
        Assert.DoesNotContain("<", text);
    }

    [Fact]
    public void Markdown_LosesItsMarkers()
    {
        var text = MarkupText.FromMarkdown("# Heading\n\nSome **bold** and _italic_ and `code` with a [link](http://x.y).\n\n- item\n");
        Assert.Contains("Heading", text);
        Assert.Contains("Some bold and italic and code with a link.", text);
        Assert.DoesNotContain("**", text);
        Assert.DoesNotContain("](", text);
    }

    // ---- icons
    [Fact]
    public void Ico_WrapsPngs_WithAValidDirectory()
    {
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 1, 2, 3 };
        var ico = IcoWriter.FromPngs(new[] { new IcoImage(png, 32, 32), new IcoImage(png, 256, 256) });
        Assert.Equal(new byte[] { 0, 0, 1, 0, 2, 0 }, ico[..6]);        // reserved, type=icon, 2 images
        Assert.Equal(32, ico[6]);                                         // first entry width
        Assert.Equal(0, ico[22]);                                         // 256 is stored as 0
        Assert.Equal(6 + 2 * 16, BitConverter.ToInt32(ico, 18));          // first image offset
        Assert.Equal(6 + 2 * 16 + png.Length, BitConverter.ToInt32(ico, 34));
    }

    // ---- zip
    [Fact]
    public void Zip_CreatesFromAFolder_AndExtractsBack()
    {
        var src = Path.Combine(_dir, "src"); Directory.CreateDirectory(Path.Combine(src, "sub"));
        File.WriteAllText(Path.Combine(src, "a.txt"), "A"); File.WriteAllText(Path.Combine(src, "sub", "b.txt"), "B");
        var zip = Path.Combine(_dir, "out", "src.zip");
        ZipTool.Create(src, zip);
        var dest = Path.Combine(_dir, "unzipped");
        ZipTool.Extract(zip, dest);
        Assert.Equal("A", File.ReadAllText(Path.Combine(dest, "a.txt")));
        Assert.Equal("B", File.ReadAllText(Path.Combine(dest, "sub", "b.txt")));
    }

    [Fact]
    public void Zip_CreatesFromASingleFile()
    {
        var file = Path.Combine(_dir, "one.txt"); File.WriteAllText(file, "x");
        var zip = Path.Combine(_dir, "one.zip");
        ZipTool.Create(file, zip);
        using var archive = ZipFile.OpenRead(zip);
        Assert.Equal("one.txt", Assert.Single(archive.Entries).FullName);
    }

    [Fact]
    public void Extract_BlocksEntriesThatEscapeTheFolder()
    {
        var zip = Path.Combine(_dir, "evil.zip");
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            var e = archive.CreateEntry("../../escaped.txt");
            using (var w = new StreamWriter(e.Open())) w.Write("nope");
            var ok = archive.CreateEntry("fine.txt");
            using (var w = new StreamWriter(ok.Open())) w.Write("ok");
        }
        var dest = Path.Combine(_dir, "safe");
        var skipped = ZipTool.Extract(zip, dest);
        Assert.Equal(1, skipped);
        Assert.True(File.Exists(Path.Combine(dest, "fine.txt")));
        Assert.False(File.Exists(Path.Combine(_dir, "escaped.txt")));
        Assert.False(File.Exists(Path.Combine(Directory.GetParent(_dir)!.FullName, "escaped.txt")));
    }

    [Fact]
    public void Extract_RefusesATooLargeArchive()
    {
        var zip = Path.Combine(_dir, "big.zip");
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            var e = archive.CreateEntry("zeros.bin");
            using var s = e.Open();
            var chunk = new byte[1024 * 1024];
            for (var i = 0; i < 12; i++) s.Write(chunk); // 12 MB of zeros, compresses tiny
        }
        Assert.Throws<InvalidDataException>(() => ZipTool.Extract(zip, Path.Combine(_dir, "bomb"), maxTotalBytes: 5 * 1024 * 1024));
    }
}
