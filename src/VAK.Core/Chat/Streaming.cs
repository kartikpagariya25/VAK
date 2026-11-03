using System.Text;
using System.Text.Json;

namespace VAK.Core.Chat;

public record SseEvent(string? Name, string Data);

/// <summary>Reads server-sent events from text that arrives in arbitrary pieces.</summary>
public sealed class SseParser
{
    private string _buffer = "";
    private string? _name;
    private readonly StringBuilder _data = new();
    private bool _hasData;

    public IEnumerable<SseEvent> Feed(string chunk)
    {
        _buffer = (_buffer + chunk).Replace("\r\n", "\n");
        var events = new List<SseEvent>();
        int newline;
        while ((newline = _buffer.IndexOf('\n')) >= 0)
        {
            var line = _buffer[..newline];
            _buffer = _buffer[(newline + 1)..];
            if (line.Length == 0)
            {
                if (_hasData) events.Add(new SseEvent(_name, _data.ToString()));
                _name = null; _data.Clear(); _hasData = false;
            }
            else if (line[0] == ':') { }
            else if (line.StartsWith("event:")) _name = line[6..].TrimStart();
            else if (line.StartsWith("data:"))
            {
                if (_hasData) _data.Append('\n');
                _data.Append(line[5..].TrimStart());
                _hasData = true;
            }
        }
        return events;
    }
}

/// <summary>Request bodies and stream parsing for the two providers.</summary>
public static class ProviderFormats
{
    public const int MaxTokens = 8192;

    private static string FileBlock(ChatAttachment a) => $"File: {a.Name}\n```\n{a.Text}\n```";

    public static string AnthropicRequest(string model, IEnumerable<ChatMessage> messages)
    {
        var list = new List<object>();
        foreach (var m in messages)
        {
            if (m.Role == "assistant" || m.Attachments.Count == 0)
            {
                list.Add(new { role = m.Role, content = m.Text });
                continue;
            }
            var parts = new List<object>();
            foreach (var a in m.Attachments.Where(a => a.Kind == "image"))
                parts.Add(new { type = "image", source = new { type = "base64", media_type = a.MediaType, data = a.Data } });
            foreach (var a in m.Attachments.Where(a => a.Kind == "text"))
                parts.Add(new { type = "text", text = FileBlock(a) });
            parts.Add(new { type = "text", text = m.Text.Length == 0 ? "(see attachments)" : m.Text });
            list.Add(new { role = "user", content = parts });
        }
        return JsonSerializer.Serialize(new { model, max_tokens = MaxTokens, stream = true, messages = list });
    }

    public static string OpenAiRequest(string model, IEnumerable<ChatMessage> messages)
    {
        var list = new List<object>();
        foreach (var m in messages)
        {
            if (m.Role == "assistant" || m.Attachments.Count == 0)
            {
                list.Add(new { role = m.Role, content = m.Text });
                continue;
            }
            var text = string.Join("\n\n", m.Attachments.Where(a => a.Kind == "text").Select(FileBlock).Append(m.Text.Length == 0 ? "(see attachments)" : m.Text));
            var parts = new List<object> { new { type = "text", text } };
            foreach (var a in m.Attachments.Where(a => a.Kind == "image"))
                parts.Add(new { type = "image_url", image_url = new { url = $"data:{a.MediaType};base64,{a.Data}" } });
            list.Add(new { role = "user", content = parts });
        }
        return JsonSerializer.Serialize(new { model, stream = true, messages = list });
    }

    public static string? AnthropicDelta(SseEvent e)
    {
        try
        {
            using var doc = JsonDocument.Parse(e.Data);
            if (doc.RootElement.TryGetProperty("delta", out var delta)
                && delta.TryGetProperty("type", out var type) && type.GetString() == "text_delta"
                && delta.TryGetProperty("text", out var text)) return text.GetString();
        }
        catch (JsonException) { }
        return null;
    }

    public static string? OpenAiDelta(SseEvent e)
    {
        if (e.Data == "[DONE]") return null;
        try
        {
            using var doc = JsonDocument.Parse(e.Data);
            if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0
                && choices[0].TryGetProperty("delta", out var delta)
                && delta.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String) return content.GetString();
        }
        catch (JsonException) { }
        return null;
    }
}
