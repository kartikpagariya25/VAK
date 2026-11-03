using System.Text.RegularExpressions;
using VAK.Core.Storage;

namespace VAK.Core.Links;

public class LinkItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public DateTime Added { get; set; }
}

public static class LinkNormalizer
{
    private static readonly Regex HostPort = new(@"^[\w.-]+:\d+(/.*)?$", RegexOptions.Compiled);

    /// <summary>Returns a clean http(s) address, or null. Only http and https are ever accepted
    /// (javascript:, file:, ftp: and friends are rejected), and a bare host such as "example.com" gets https://.</summary>
    public static string? Normalize(string? input)
    {
        var text = input?.Trim();
        if (string.IsNullOrEmpty(text) || text.Any(char.IsWhiteSpace)) return null;

        if (!text.Contains("://"))
        {
            var looksLikeHost = text.Contains('.') || text.StartsWith("localhost", StringComparison.OrdinalIgnoreCase);
            if (text.Contains(':') && !HostPort.IsMatch(text)) return null; // javascript:..., mailto:... and similar
            if (!looksLikeHost) return null;
            text = "https://" + text;
        }
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri)) return null;
        return uri.Scheme is "http" or "https" ? uri.AbsoluteUri : null;
    }

    /// <summary>The host without "www.", used as a default title.</summary>
    public static string HostTitle(string normalizedUrl)
    {
        var host = new Uri(normalizedUrl).Host;
        return host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;
    }
}

public record SearchEngine(string Id, string Name, string Template);

public static class SearchEngines
{
    public static IReadOnlyList<SearchEngine> All { get; } = new SearchEngine[]
    {
        new("google", "Google", "https://www.google.com/search?q={0}"),
        new("youtube", "YouTube", "https://www.youtube.com/results?search_query={0}"),
        new("github", "GitHub", "https://github.com/search?q={0}"),
        new("stackoverflow", "Stack Overflow", "https://stackoverflow.com/search?q={0}"),
        new("chatgpt", "ChatGPT", "https://chatgpt.com/?q={0}"),
        new("perplexity", "Perplexity", "https://www.perplexity.ai/search?q={0}"),
    };

    public static string? BuildUrl(string engineId, string query)
    {
        var engine = All.FirstOrDefault(e => e.Id == engineId);
        return engine is null ? null : string.Format(engine.Template, Uri.EscapeDataString(query));
    }
}

/// <summary>The saved links. Every change is saved straight away.</summary>
public class LinksRepository
{
    private readonly JsonListStore<LinkItem> _store;
    private readonly List<LinkItem> _items;

    public LinksRepository(string path)
    {
        _store = new JsonListStore<LinkItem>(path);
        _items = _store.Load();
    }

    public IReadOnlyList<LinkItem> All => _items.OrderByDescending(l => l.Added).ToList();

    /// <summary>Adds a link. Returns null when the address is not http(s) or is already saved.</summary>
    public LinkItem? Add(string url, string? title)
    {
        var normalized = LinkNormalizer.Normalize(url);
        if (normalized is null || _items.Any(l => string.Equals(l.Url, normalized, StringComparison.OrdinalIgnoreCase))) return null;
        var link = new LinkItem
        {
            Url = normalized, Added = DateTime.Now,
            Title = string.IsNullOrWhiteSpace(title) ? LinkNormalizer.HostTitle(normalized) : title.Trim(),
        };
        _items.Add(link);
        _store.Save(_items);
        return link;
    }

    public void Remove(string id)
    {
        if (_items.RemoveAll(l => l.Id == id) > 0) _store.Save(_items);
    }
}
