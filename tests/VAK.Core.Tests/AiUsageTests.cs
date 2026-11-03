using VAK.Core.AiUsage;
using Xunit;

namespace VAK.Core.Tests;

public class AiUsageTests
{
    private const string UserPrompt = "{\"type\":\"user\",\"timestamp\":\"2026-11-03T08:30:00.000Z\",\"sessionId\":\"s1\",\"cwd\":\"C:/Dev/VAK\",\"message\":{\"role\":\"user\",\"content\":\"hello\"}}";
    private const string ToolResultLine = "{\"type\":\"user\",\"timestamp\":\"2026-11-03T08:30:05.000Z\",\"sessionId\":\"s1\",\"message\":{\"role\":\"user\",\"content\":[{\"type\":\"tool_result\",\"tool_use_id\":\"t1\",\"content\":\"ok\"}]}}";
    private const string Assistant = "{\"type\":\"assistant\",\"timestamp\":\"2026-11-03T08:30:10.000Z\",\"sessionId\":\"s1\",\"cwd\":\"C:/Dev/VAK\",\"message\":{\"id\":\"m1\",\"role\":\"assistant\",\"model\":\"claude-opus-4-5\",\"content\":[{\"type\":\"text\",\"text\":\"hi\"},{\"type\":\"tool_use\",\"id\":\"t1\",\"name\":\"Bash\"}],\"usage\":{\"input_tokens\":100,\"output_tokens\":50,\"cache_creation_input_tokens\":1000,\"cache_read_input_tokens\":2000}}}";

    [Fact]
    public void UserPrompt_CountsAsOneMessage_NoTokens()
    {
        var e = new ClaudeLogParser().ParseLine(UserPrompt)!;
        Assert.Equal((AiAgent.Claude, "s1", 1, 0, 0L), (e.Agent, e.SessionId, e.Messages, e.ToolCalls, e.TotalTokens));
        Assert.Equal("VAK", e.Project);
    }

    [Fact]
    public void ToolResultLine_IsNotAMessage() => Assert.Null(new ClaudeLogParser().ParseLine(ToolResultLine));

    [Fact]
    public void Assistant_ReadsUsageModelAndToolCalls()
    {
        var e = new ClaudeLogParser().ParseLine(Assistant)!;
        Assert.Equal((100L, 50L, 1000L, 2000L), (e.Input, e.Output, e.CacheWrite, e.CacheRead));
        Assert.Equal(3150, e.TotalTokens);
        Assert.Equal(1, e.ToolCalls);
        Assert.Equal("claude-opus-4-5", e.Model);
        Assert.Equal(0, e.Messages);
    }

    [Fact]
    public void RepeatedAssistantLine_IsCountedOnce()
    {
        var p = new ClaudeLogParser();
        Assert.NotNull(p.ParseLine(Assistant));
        Assert.Null(p.ParseLine(Assistant));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"type\":\"summary\"}")]
    [InlineData("{\"type\":\"assistant\"}")]
    public void BadOrIrrelevantLines_ReturnNull(string line) => Assert.Null(new ClaudeLogParser().ParseLine(line));

    [Fact]
    public void Timestamps_AreConvertedToLocalTime()
    {
        var e = new ClaudeLogParser().ParseLine(UserPrompt)!;
        Assert.Equal(DateTime.Parse("2026-11-03T08:30:00Z").ToUniversalTime().ToLocalTime(), e.Time);
    }

    [Theory]
    [InlineData("claude-opus-4-5", 1_000_000, 0, 0, 0, 15.0)]
    [InlineData("claude-sonnet-4-5", 0, 1_000_000, 0, 0, 15.0)]
    [InlineData("claude-haiku-4-5", 1_000_000, 0, 0, 0, 1.0)]
    [InlineData("claude-opus-4-5", 0, 0, 1_000_000, 0, 18.75)]
    [InlineData("claude-opus-4-5", 0, 0, 0, 1_000_000, 1.5)]
    [InlineData("something-new", 1_000_000, 0, 0, 0, 3.0)]
    public void Cost_UsesModelFamilyPrices(string model, long input, long output, long write, long read, double expected) =>
        Assert.Equal(expected, ModelPricing.Cost(model, input, output, write, read), 6);

    private static UsageEvent Ev(int day, int hour, AiAgent agent = AiAgent.Claude, string session = "s", string project = "P",
        long tokens = 100, int msgs = 0, int tools = 0) =>
        new(new DateTime(2026, 11, day, hour, 0, 0), agent, session, project, "claude-sonnet-4-5", tokens, 0, 0, 0, msgs, tools);

    [Fact]
    public void Summary_AggregatesTheDay()
    {
        var events = new[]
        {
            Ev(3, 9, session: "a", msgs: 1), Ev(3, 9, session: "a", tools: 2, tokens: 1_000_000),
            Ev(3, 14, session: "b", project: "Q", msgs: 1), Ev(2, 9, session: "c", msgs: 5),
            Ev(3, 10, AiAgent.Codex, "x", "P", 500),
        };
        var s = AiUsageSummary.ForDay(events, new DateTime(2026, 11, 3), null);
        Assert.Equal(2, s.Messages);
        Assert.Equal(2, s.ToolCalls);
        Assert.Equal(3, s.Sessions);
        Assert.Equal(1_000_000 + 100 + 100 + 500, s.Tokens);
        Assert.Equal("P", s.TopProject);
        Assert.Equal(1, s.ActiveHours[9]);
        Assert.Equal(1, s.ActiveHours[14]);
        Assert.Equal(0, s.ActiveHours[3]);
    }

    [Fact]
    public void Summary_FiltersByAgent()
    {
        var events = new[] { Ev(3, 9, AiAgent.Claude, tokens: 10), Ev(3, 9, AiAgent.Codex, tokens: 20) };
        Assert.Equal(20, AiUsageSummary.ForDay(events, new DateTime(2026, 11, 3), AiAgent.Codex).Tokens);
        Assert.Equal(30, AiUsageSummary.ForDay(events, new DateTime(2026, 11, 3), null).Tokens);
    }

    [Fact]
    public void Streak_CountsConsecutiveActiveDaysEndingToday()
    {
        var events = new[] { Ev(3, 9), Ev(2, 9), Ev(1, 9), Ev(10 - 9 + 28 - 28, 9) };
        Assert.Equal(3, AiUsageSummary.Streak(events, new DateTime(2026, 11, 3), null));
        Assert.Equal(0, AiUsageSummary.Streak(new[] { Ev(1, 9) }, new DateTime(2026, 11, 3), null));
    }

    [Fact]
    public void MonthCost_SumsOnlyThatMonth()
    {
        var events = new[]
        {
            new UsageEvent(new DateTime(2026, 11, 2), AiAgent.Claude, "a", "P", "claude-sonnet-4-5", 1_000_000, 0, 0, 0, 0, 0),
            new UsageEvent(new DateTime(2026, 10, 30), AiAgent.Claude, "a", "P", "claude-sonnet-4-5", 1_000_000, 0, 0, 0, 0, 0),
        };
        Assert.Equal(3.0, AiUsageSummary.MonthCost(events, new DateTime(2026, 11, 3), null), 6);
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(950, "950")]
    [InlineData(12400, "12.4K")]
    [InlineData(999999, "1M")]
    [InlineData(3200000, "3.2M")]
    [InlineData(1000000000, "1B")]
    public void FormatTokens_Abbreviates(long tokens, string expected) => Assert.Equal(expected, AiUsageSummary.FormatTokens(tokens));

    private static string Prompt(string uuid, string extra = "") =>
        "{\"type\":\"user\",\"uuid\":\"" + uuid + "\"," + extra + "\"timestamp\":\"2026-11-03T08:30:00.000Z\",\"sessionId\":\"s1\",\"message\":{\"role\":\"user\",\"content\":\"hello\"}}";

    [Fact]
    public void RepeatedPrompt_SameUuid_IsCountedOnce()
    {
        var p = new ClaudeLogParser();
        Assert.NotNull(p.ParseLine(Prompt("u1")));
        Assert.Null(p.ParseLine(Prompt("u1")));
        Assert.NotNull(p.ParseLine(Prompt("u2")));
    }

    [Theory]
    [InlineData("\"isMeta\":true,")]
    [InlineData("\"isCompactSummary\":true,")]
    public void MetaPrompts_AreNotMessages(string extra) => Assert.Null(new ClaudeLogParser().ParseLine(Prompt("u9", extra)));

    [Fact]
    public void Events_CarryAnIdForCrossFileDedupe()
    {
        Assert.Equal("m1", new ClaudeLogParser().ParseLine(Assistant)!.Id);
        Assert.Equal("u1", new ClaudeLogParser().ParseLine(Prompt("u1"))!.Id);
    }
}
