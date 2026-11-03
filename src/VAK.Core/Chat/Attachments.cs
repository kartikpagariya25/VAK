namespace VAK.Core.Chat;

public record AttachmentResult(ChatAttachment? Attachment, string? Problem);

/// <summary>Turns a dropped or picked file into something that can go in a message: an image (up to 5 MB) or a text file (up to 200 KB).</summary>
public static class AttachmentReader
{
    public const int MaxImageBytes = 5 * 1024 * 1024;
    public const int MaxTextBytes = 200 * 1024;

    private static readonly Dictionary<string, string> Images = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png", [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".gif"] = "image/gif", [".webp"] = "image/webp",
    };

    private static readonly HashSet<string> TextTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".json", ".csv", ".xml", ".yaml", ".yml", ".log", ".cs", ".js", ".ts", ".tsx", ".jsx", ".py", ".html", ".css", ".sql",
        ".sh", ".ps1", ".ini", ".toml", ".cfg", ".java", ".c", ".cpp", ".h", ".rs", ".go", ".rb", ".php", ".kt", ".swift", ".xaml", ".csproj",
    };

    public static AttachmentResult Read(string path)
    {
        var name = Path.GetFileName(path);
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists) return new(null, $"{name} was not found.");
            var ext = info.Extension;
            if (Images.TryGetValue(ext, out var media))
            {
                if (info.Length > MaxImageBytes) return new(null, $"{name} is larger than 5 MB.");
                return new(new ChatAttachment { Kind = "image", Name = name, MediaType = media, Data = Convert.ToBase64String(File.ReadAllBytes(path)) }, null);
            }
            if (TextTypes.Contains(ext))
            {
                if (info.Length > MaxTextBytes) return new(null, $"{name} is larger than 200 KB.");
                return new(new ChatAttachment { Kind = "text", Name = name, Text = File.ReadAllText(path) }, null);
            }
            return new(null, $"{name}: only images and plain text or code files can be attached.");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new(null, $"{name} could not be read.");
        }
    }
}
