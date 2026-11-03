using VAK.Core.AiUsage;
using Xunit;

namespace VAK.Core.Tests;

public class AiUsageCollectorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "vak-ai-" + Guid.NewGuid().ToString("N"));
    private static readonly DateTime Now = new(2026, 11, 3, 15, 0, 0, DateTimeKind.Local);

    public AiUsageCollectorTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    private static string Utc(DateTime local) => local.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

    private string WriteClaude(string name, params string[] lines)
    {
        var dir = Path.Combine(_root, "claude", "proj");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name);
        File.WriteAllLines(path, lines);
        File.SetLastWriteTime(path, Now);
        return path;
    }

    private static string AssistantLine(string id, DateTime t, int input) =>
        "{\"type\":\"assistant\",\"timestamp\":\"" + Utc(t) + "\",\"sessionId\":\"s\",\"cwd\":\"C:/Dev/VAK\",\"message\":{\"id\":\"" + id +
        "\",\"model\":\"claude-sonnet-4-5\",\"content\":[],\"usage\":{\"input_tokens\":" + input + ",\"output_tokens\":0}}}";

    [Fact]
    public void Collect_ReadsClaudeFiles()
    {
        WriteClaude("a.jsonl", AssistantLine("m1", Now, 100), AssistantLine("m2", Now.AddMinutes(1), 200));
        var events = new AiUsageCollector(Path.Combine(_root, "claude"), Path.Combine(_root, "codex"), () => Now).Collect();
        Assert.Equal(300, events.Sum(e => e.TotalTokens));
    }

    [Fact]
    public void Collect_SameMessageInTwoFiles_IsCountedOnce()
    {
        WriteClaude("a.jsonl", AssistantLine("m1", Now, 100));
        WriteClaude("b.jsonl", AssistantLine("m1", Now, 100)); // a resumed session repeats history
        var events = new AiUsageCollector(Path.Combine(_root, "claude"), Path.Combine(_root, "codex"), () => Now).Collect();
        Assert.Equal(100, events.Sum(e => e.TotalTokens));
    }

    [Fact]
    public void Collect_MissingFolders_ReturnsEmpty() =>
        Assert.Empty(new AiUsageCollector(Path.Combine(_root, "nope1"), Path.Combine(_root, "nope2"), () => Now).Collect());

    [Fact]
    public void Collect_IgnoresOldFiles()
    {
        var path = WriteClaude("old.jsonl", AssistantLine("m1", Now.AddDays(-90), 100));
        File.SetLastWriteTime(path, Now.AddDays(-90));
        Assert.Empty(new AiUsageCollector(Path.Combine(_root, "claude"), Path.Combine(_root, "codex"), () => Now).Collect());
    }

    [Fact]
    public void Collect_IsReadableWhileAnotherProcessHasTheFileOpen()
    {
        var path = WriteClaude("live.jsonl", AssistantLine("m1", Now, 100));
        using var writer = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
        var events = new AiUsageCollector(Path.Combine(_root, "claude"), Path.Combine(_root, "codex"), () => Now).Collect();
        Assert.Equal(100, events.Sum(e => e.TotalTokens));
    }

    [Fact]
    public void Collect_SkipsUnparseableLines()
    {
        WriteClaude("a.jsonl", "garbage", AssistantLine("m1", Now, 100), "{\"broken");
        var events = new AiUsageCollector(Path.Combine(_root, "claude"), Path.Combine(_root, "codex"), () => Now).Collect();
        Assert.Equal(100, events.Sum(e => e.TotalTokens));
    }
}
