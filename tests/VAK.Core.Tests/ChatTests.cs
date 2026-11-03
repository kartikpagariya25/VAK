using System.Net;
using System.Text;
using VAK.Core.Chat;
using Xunit;

namespace VAK.Core.Tests;

public class ChatTests
{
    private static string Temp() => Path.Combine(Path.GetTempPath(), "vak-chat-" + Guid.NewGuid().ToString("N"));

    // ---- API key vault ----
    [Fact]
    public void Vault_round_trips_a_key_and_does_not_store_it_in_plain_text()
    {
        var path = Temp();
        var vault = new ApiKeyVault(path);
        vault.Set("claude", "sk-ant-secret-123");
        Assert.True(vault.Has("claude"));
        Assert.Equal("sk-ant-secret-123", new ApiKeyVault(path).Get("claude"));
        Assert.DoesNotContain("sk-ant-secret-123", File.ReadAllText(path));
    }

    [Fact]
    public void Vault_remove_forgets_the_key()
    {
        var vault = new ApiKeyVault(Temp());
        vault.Set("gpt", "sk-1");
        vault.Remove("gpt");
        Assert.False(vault.Has("gpt"));
        Assert.Null(vault.Get("gpt"));
    }

    [Fact]
    public void Vault_survives_a_corrupt_file()
    {
        var path = Temp();
        File.WriteAllText(path, "not json {{{");
        var vault = new ApiKeyVault(path);
        Assert.False(vault.Has("claude"));
        vault.Set("claude", "k");
        Assert.Equal("k", vault.Get("claude"));
    }

    // ---- conversations ----
    [Fact]
    public void Store_saves_lists_newest_first_and_deletes()
    {
        var store = new ChatStore(Temp());
        var a = new Conversation { Title = "A", Updated = new DateTime(2026, 1, 1) };
        var b = new Conversation { Title = "B", Updated = new DateTime(2026, 2, 1) };
        store.Save(a); store.Save(b);
        Assert.Equal(new[] { "B", "A" }, store.All().Select(c => c.Title));
        store.Delete(b.Id);
        Assert.Single(store.All());
    }

    [Fact]
    public void Store_round_trips_messages_and_attachments()
    {
        var path = Temp();
        var c = new Conversation { Title = "T" };
        c.Messages.Add(new ChatMessage { Role = "user", Text = "hi", Attachments = { new ChatAttachment { Kind = "image", Name = "a.png", MediaType = "image/png", Data = "AAAA" } } });
        new ChatStore(path).Save(c);
        var back = new ChatStore(path).All().Single();
        Assert.Equal("hi", back.Messages[0].Text);
        Assert.Equal("AAAA", back.Messages[0].Attachments[0].Data);
    }

    [Fact]
    public void Title_comes_from_the_first_user_message_and_is_shortened()
    {
        Assert.Equal("New chat", Conversation.TitleFrom(""));
        var t = Conversation.TitleFrom(new string('x', 200));
        Assert.True(t.Length <= 41);
        Assert.Equal("hello world", Conversation.TitleFrom("hello\nworld"));
    }

    // ---- server-sent events ----
    [Fact]
    public void Sse_parser_reads_event_and_data_across_chunks()
    {
        var p = new SseParser();
        var events = new List<SseEvent>();
        events.AddRange(p.Feed("event: content_block_delta\ndata: {\"a\":1}\n"));
        Assert.Empty(events);
        events.AddRange(p.Feed("\ndata: [DONE]\n\n"));
        Assert.Equal(2, events.Count);
        Assert.Equal("content_block_delta", events[0].Name);
        Assert.Equal("{\"a\":1}", events[0].Data);
        Assert.Equal("[DONE]", events[1].Data);
    }

    [Fact]
    public void Sse_parser_handles_crlf_and_comments()
    {
        var events = new SseParser().Feed(": ping\r\ndata: x\r\n\r\n").ToList();
        Assert.Single(events);
        Assert.Equal("x", events[0].Data);
    }

    // ---- request bodies and stream parsing ----
    private static Conversation Sample()
    {
        var c = new Conversation { Provider = "claude", Model = "m" };
        c.Messages.Add(new ChatMessage { Role = "user", Text = "look", Attachments = { new ChatAttachment { Kind = "image", Name = "p.png", MediaType = "image/png", Data = "QUJD" }, new ChatAttachment { Kind = "text", Name = "n.txt", Text = "file body" } } });
        c.Messages.Add(new ChatMessage { Role = "assistant", Text = "ok" });
        return c;
    }

    [Fact]
    public void Anthropic_request_has_stream_image_blocks_and_text_files_inlined()
    {
        var json = System.Text.Json.JsonDocument.Parse(ProviderFormats.AnthropicRequest("claude-x", Sample().Messages));
        var root = json.RootElement;
        Assert.True(root.GetProperty("stream").GetBoolean());
        Assert.Equal("claude-x", root.GetProperty("model").GetString());
        var content = root.GetProperty("messages")[0].GetProperty("content");
        Assert.Equal("image", content[0].GetProperty("type").GetString());
        Assert.Equal("QUJD", content[0].GetProperty("source").GetProperty("data").GetString());
        Assert.Contains("file body", content[1].GetProperty("text").GetString());
        Assert.Equal("ok", root.GetProperty("messages")[1].GetProperty("content").GetString());
    }

    [Fact]
    public void OpenAi_request_uses_image_data_urls()
    {
        var json = System.Text.Json.JsonDocument.Parse(ProviderFormats.OpenAiRequest("gpt-x", Sample().Messages));
        var content = json.RootElement.GetProperty("messages")[0].GetProperty("content");
        Assert.Contains("data:image/png;base64,QUJD", content[1].GetProperty("image_url").GetProperty("url").GetString());
        Assert.True(json.RootElement.GetProperty("stream").GetBoolean());
    }

    [Fact]
    public void Delta_text_is_read_from_each_provider_format()
    {
        Assert.Equal("Hi", ProviderFormats.AnthropicDelta(new SseEvent("content_block_delta", "{\"type\":\"content_block_delta\",\"delta\":{\"type\":\"text_delta\",\"text\":\"Hi\"}}")));
        Assert.Null(ProviderFormats.AnthropicDelta(new SseEvent("message_start", "{\"type\":\"message_start\"}")));
        Assert.Equal("Yo", ProviderFormats.OpenAiDelta(new SseEvent(null, "{\"choices\":[{\"delta\":{\"content\":\"Yo\"}}]}")));
        Assert.Null(ProviderFormats.OpenAiDelta(new SseEvent(null, "[DONE]")));
        Assert.Null(ProviderFormats.OpenAiDelta(new SseEvent(null, "{\"choices\":[]}")));
    }

    // ---- the client ----
    private sealed class Fake : HttpMessageHandler
    {
        public HttpRequestMessage? Seen;
        private readonly HttpStatusCode _code; private readonly string _body;
        public Fake(HttpStatusCode code, string body) { _code = code; _body = body; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Seen = request;
            return Task.FromResult(new HttpResponseMessage(_code) { Content = new StringContent(_body, Encoding.UTF8, "text/event-stream") });
        }
    }

    private static async Task<List<string>> Collect(ChatClient client, string provider)
    {
        var parts = new List<string>();
        await foreach (var s in client.StreamAsync(provider, "key-1", "model-1", Sample().Messages)) parts.Add(s);
        return parts;
    }

    [Fact]
    public async Task Client_streams_claude_text_and_sends_the_key_in_the_header()
    {
        var body = "event: content_block_delta\ndata: {\"delta\":{\"type\":\"text_delta\",\"text\":\"Hel\"}}\n\nevent: content_block_delta\ndata: {\"delta\":{\"type\":\"text_delta\",\"text\":\"lo\"}}\n\n";
        var fake = new Fake(HttpStatusCode.OK, body);
        var parts = await Collect(new ChatClient(fake), "claude");
        Assert.Equal("Hello", string.Concat(parts));
        Assert.Equal("api.anthropic.com", fake.Seen!.RequestUri!.Host);
        Assert.Equal("key-1", fake.Seen.Headers.GetValues("x-api-key").Single());
    }

    [Fact]
    public async Task Client_streams_gpt_text_with_a_bearer_token()
    {
        var body = "data: {\"choices\":[{\"delta\":{\"content\":\"A\"}}]}\n\ndata: {\"choices\":[{\"delta\":{\"content\":\"B\"}}]}\n\ndata: [DONE]\n\n";
        var fake = new Fake(HttpStatusCode.OK, body);
        Assert.Equal("AB", string.Concat(await Collect(new ChatClient(fake), "gpt")));
        Assert.Equal("api.openai.com", fake.Seen!.RequestUri!.Host);
        Assert.Equal("Bearer key-1", fake.Seen.Headers.Authorization!.ToString());
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "key")]
    [InlineData(HttpStatusCode.TooManyRequests, "limit")]
    [InlineData(HttpStatusCode.InternalServerError, "try again")]
    public async Task Client_turns_http_errors_into_friendly_messages(HttpStatusCode code, string expected)
    {
        var ex = await Assert.ThrowsAsync<ChatException>(() => Collect(new ChatClient(new Fake(code, "{\"error\":{\"message\":\"bad\"}}")), "claude"));
        Assert.Contains(expected, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ---- attachments ----
    [Fact]
    public void Attachment_classifies_images_text_and_rejects_the_rest()
    {
        var dir = Temp(); Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, "a.png"), new byte[] { 1, 2, 3 });
        File.WriteAllText(Path.Combine(dir, "n.md"), "# hi");
        File.WriteAllBytes(Path.Combine(dir, "b.exe"), new byte[] { 1 });
        var img = AttachmentReader.Read(Path.Combine(dir, "a.png"));
        Assert.Equal("image", img.Attachment!.Kind); Assert.Equal("image/png", img.Attachment.MediaType); Assert.Equal(Convert.ToBase64String(new byte[] { 1, 2, 3 }), img.Attachment.Data);
        var txt = AttachmentReader.Read(Path.Combine(dir, "n.md"));
        Assert.Equal("text", txt.Attachment!.Kind); Assert.Equal("# hi", txt.Attachment.Text);
        var bad = AttachmentReader.Read(Path.Combine(dir, "b.exe"));
        Assert.Null(bad.Attachment); Assert.NotNull(bad.Problem);
    }

    [Fact]
    public void Attachment_rejects_oversized_files()
    {
        var dir = Temp(); Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, "big.png"), new byte[AttachmentReader.MaxImageBytes + 1]);
        File.WriteAllText(Path.Combine(dir, "big.txt"), new string('x', AttachmentReader.MaxTextBytes + 1));
        Assert.NotNull(AttachmentReader.Read(Path.Combine(dir, "big.png")).Problem);
        Assert.NotNull(AttachmentReader.Read(Path.Combine(dir, "big.txt")).Problem);
        Assert.NotNull(AttachmentReader.Read(Path.Combine(dir, "missing.txt")).Problem);
    }

    // ---- markdown ----
    [Fact]
    public void Markdown_splits_code_fences_headings_and_lists()
    {
        var blocks = MarkdownParser.Parse("# Title\n\nSome **bold** text.\n\n- one\n- two\n\n```csharp\nvar x = 1;\n```\n").ToList();
        Assert.Equal(new[] { MdKind.Heading, MdKind.Paragraph, MdKind.Bullet, MdKind.Bullet, MdKind.Code }, blocks.Select(b => b.Kind));
        Assert.Equal("csharp", blocks[4].Language);
        Assert.Equal("var x = 1;", blocks[4].Text);
    }

    [Fact]
    public void Markdown_keeps_an_unclosed_fence_as_code_while_streaming()
    {
        var blocks = MarkdownParser.Parse("text\n```py\nprint(1)").ToList();
        Assert.Equal(MdKind.Code, blocks[1].Kind);
        Assert.Equal("print(1)", blocks[1].Text);
    }

    [Fact]
    public void Markdown_inline_finds_bold_italic_and_code()
    {
        var runs = MarkdownParser.Inline("a **b** `c` *d* e").ToList();
        Assert.Equal(new[] { "a ", "b", " ", "c", " ", "d", " e" }, runs.Select(r => r.Text));
        Assert.True(runs[1].Bold); Assert.True(runs[3].Code); Assert.True(runs[5].Italic);
    }

    [Fact]
    public void Markdown_inline_leaves_unmatched_markers_alone()
    {
        Assert.Equal("2 * 3 and **open", string.Concat(MarkdownParser.Inline("2 * 3 and **open").Select(r => r.Text)));
    }
}
