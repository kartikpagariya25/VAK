using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace VAK.Core.Chat;

public sealed class ChatException : Exception
{
    public ChatException(string message) : base(message) { }
}

/// <summary>Streams a reply from the Anthropic or OpenAI API. The only hosts it talks to are api.anthropic.com and api.openai.com.</summary>
public sealed class ChatClient
{
    private readonly HttpClient _http;

    public ChatClient(HttpMessageHandler? handler = null)
    {
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = Timeout.InfiniteTimeSpan;
    }

    public async IAsyncEnumerable<string> StreamAsync(string provider, string apiKey, string model, IEnumerable<ChatMessage> messages,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var claude = provider != "gpt";
        var name = claude ? "Claude" : "OpenAI";
        using var request = new HttpRequestMessage(HttpMethod.Post, claude ? "https://api.anthropic.com/v1/messages" : "https://api.openai.com/v1/chat/completions");
        if (claude)
        {
            request.Headers.Add("x-api-key", apiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");
        }
        else request.Headers.Authorization = new("Bearer", apiKey);
        request.Content = new StringContent(claude ? ProviderFormats.AnthropicRequest(model, messages) : ProviderFormats.OpenAiRequest(model, messages), Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try { response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false); }
        catch (HttpRequestException) { throw new ChatException($"Could not reach {name}. Check your internet connection."); }

        using (response)
        {
            if (!response.IsSuccessStatusCode) throw new ChatException(await Friendly(response, name).ConfigureAwait(false));

            using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var parser = new SseParser();
            var buffer = new char[1024];
            int read;
            while ((read = await ReadSafe(reader, buffer, name, ct).ConfigureAwait(false)) > 0)
            {
                foreach (var e in parser.Feed(new string(buffer, 0, read)))
                {
                    if (e.Name == "error") throw new ChatException($"{name} stopped the reply: {ErrorText(e.Data)}");
                    var piece = claude ? ProviderFormats.AnthropicDelta(e) : ProviderFormats.OpenAiDelta(e);
                    if (!string.IsNullOrEmpty(piece)) yield return piece;
                }
            }
        }
    }

    private static async Task<int> ReadSafe(StreamReader reader, char[] buffer, string name, CancellationToken ct)
    {
        try { return await reader.ReadAsync(buffer.AsMemory(), ct).ConfigureAwait(false); }
        catch (IOException) { throw new ChatException($"The connection to {name} was lost."); }
    }

    private static async Task<string> Friendly(HttpResponseMessage response, string name)
    {
        var detail = "";
        try { detail = ErrorText(await response.Content.ReadAsStringAsync().ConfigureAwait(false)); } catch (Exception e) when (e is HttpRequestException or IOException) { }
        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => $"{name} rejected the API key. Check it in the key box and try again.",
            HttpStatusCode.TooManyRequests => $"{name} says the rate limit or quota was reached. Wait a moment, or check your plan and billing.",
            HttpStatusCode.NotFound => $"{name} does not know that model. Pick another model from the list.{Suffix(detail)}",
            >= HttpStatusCode.InternalServerError => $"{name} had a problem on its side. Please try again in a moment.",
            _ => $"{name} could not answer.{Suffix(detail)}",
        };
    }

    private static string Suffix(string detail) => detail.Length == 0 ? "" : " " + detail;

    private static string ErrorText(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.Object && error.TryGetProperty("message", out var m)) return m.GetString() ?? "";
                if (error.ValueKind == JsonValueKind.String) return error.GetString() ?? "";
            }
        }
        catch (JsonException) { }
        return "";
    }
}
