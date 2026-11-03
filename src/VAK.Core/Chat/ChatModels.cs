using VAK.Core.Storage;

namespace VAK.Core.Chat;

public class ChatAttachment
{
    /// <summary>"image" (base64 in Data) or "text" (contents in Text).</summary>
    public string Kind { get; set; } = "text";
    public string Name { get; set; } = "";
    public string MediaType { get; set; } = "";
    public string Data { get; set; } = "";
    public string Text { get; set; } = "";
}

public class ChatMessage
{
    /// <summary>"user" or "assistant".</summary>
    public string Role { get; set; } = "user";
    public string Text { get; set; } = "";
    public List<ChatAttachment> Attachments { get; set; } = new();
}

public class Conversation
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "New chat";
    public string Provider { get; set; } = "claude";
    public string Model { get; set; } = "";
    public DateTime Updated { get; set; } = DateTime.Now;
    public List<ChatMessage> Messages { get; set; } = new();

    public static string TitleFrom(string text)
    {
        var line = string.Join(' ', (text ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (line.Length == 0) return "New chat";
        return line.Length > 40 ? line[..40] + "…" : line;
    }
}

/// <summary>Saved conversations, newest first.</summary>
public sealed class ChatStore
{
    private readonly JsonListStore<Conversation> _store;
    private readonly List<Conversation> _items;

    public ChatStore(string path)
    {
        _store = new JsonListStore<Conversation>(path);
        _items = _store.Load();
    }

    public IReadOnlyList<Conversation> All() => _items.OrderByDescending(c => c.Updated).ToList();

    public void Save(Conversation conversation)
    {
        var i = _items.FindIndex(c => c.Id == conversation.Id);
        if (i >= 0) _items[i] = conversation; else _items.Add(conversation);
        _store.Save(_items);
    }

    public void Delete(string id)
    {
        _items.RemoveAll(c => c.Id == id);
        _store.Save(_items);
    }
}

/// <summary>Models offered in the picker. The box is editable, so a newer model id can always be typed in.</summary>
public static class ChatDefaults
{
    public static IReadOnlyList<string> Models(string provider) => provider == "gpt"
        ? new[] { "gpt-4o", "gpt-4o-mini" }
        : new[] { "claude-sonnet-5-5", "claude-opus-5-5", "claude-haiku-5-5" };

    public static string DefaultModel(string provider) => Models(provider)[0];
}
