using System.Globalization;
using System.Text.Json;

namespace VAK.Core.AiUsage;

public enum AiAgent { Claude, Codex }

public record UsageEvent(DateTime Time, AiAgent Agent, string SessionId, string Project, string Model,
    long Input, long Output, long CacheWrite, long CacheRead, int Messages, int ToolCalls, string Id = "")
{
    public long TotalTokens => Input + Output + CacheWrite + CacheRead;
    public double Cost => ModelPricing.Cost(Model, Input, Output, CacheWrite, CacheRead);
}

public static class ModelPricing
{
    /// <summary>Estimated API price in dollars. Prices are per million tokens by model family; cache writes cost 1.25x and
    /// cache reads 0.1x the input price. This is an estimate for "API value", not a bill.</summary>
    public static double Cost(string model, long input, long output, long cacheWrite, long cacheRead)
    {
        var m = (model ?? "").ToLowerInvariant();
        var (inPrice, outPrice) = m switch
        {
            _ when m.Contains("opus") => (15.0, 75.0),
            _ when m.Contains("haiku") => (1.0, 5.0),
            _ when m.Contains("sonnet") => (3.0, 15.0),
            _ when m.Contains("gpt") || m.Contains("codex") || m.StartsWith("o3") || m.StartsWith("o4") => (1.25, 10.0),
            _ => (3.0, 15.0),
        };
        return (input * inPrice + output * outPrice + cacheWrite * inPrice * 1.25 + cacheRead * inPrice * 0.1) / 1_000_000.0;
    }
}

/// <summary>Reads Claude Code session logs (JSON lines). Only numbers are kept - never message text.
/// One parser instance per run: it remembers message ids so a message split over several lines is counted once.</summary>
public class ClaudeLogParser
{
    private readonly HashSet<string> _seenMessages = new();
    private readonly HashSet<string> _seenTools = new();
    private readonly HashSet<string> _seenPrompts = new();

    public UsageEvent? ParseLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("type", out var typeEl)) return null;
            var type = typeEl.GetString();
            if (type is not ("user" or "assistant")) return null;
            if (!root.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object) return null;
            if (!TryTime(root, out var time)) return null;

            var session = Str(root, "sessionId");
            var project = ProjectName(Str(root, "cwd"));

            if (type == "user")
            {
                if (Flag(root, "isMeta") || Flag(root, "isCompactSummary") || !IsPrompt(message)) return null; // injected lines are not prompts
                var uuid = Str(root, "uuid");
                if (uuid.Length > 0 && !_seenPrompts.Add(uuid)) return null; // a resumed session repeats its history
                return new UsageEvent(time, AiAgent.Claude, session, project, "", 0, 0, 0, 0, 1, 0, uuid);
            }

            var id = Str(message, "id");
            var firstSight = id.Length == 0 || _seenMessages.Add(id);
            var tools = 0;
            if (message.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
                foreach (var block in content.EnumerateArray())
                    if (Str(block, "type") == "tool_use" && _seenTools.Add(Str(block, "id") is { Length: > 0 } tid ? tid : Guid.NewGuid().ToString()))
                        tools++;

            long input = 0, output = 0, write = 0, read = 0;
            if (firstSight && message.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object)
            {
                input = Num(usage, "input_tokens"); output = Num(usage, "output_tokens");
                write = Num(usage, "cache_creation_input_tokens"); read = Num(usage, "cache_read_input_tokens");
            }
            if (input + output + write + read == 0 && tools == 0) return null;
            return new UsageEvent(time, AiAgent.Claude, session, project, Str(message, "model"), input, output, write, read, 0, tools, firstSight ? id : "");
        }
        catch (JsonException) { return null; }
    }

    private static bool Flag(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    private static bool IsPrompt(JsonElement message)
    {
        if (!message.TryGetProperty("content", out var content)) return false;
        if (content.ValueKind == JsonValueKind.String) return !string.IsNullOrWhiteSpace(content.GetString());
        if (content.ValueKind != JsonValueKind.Array) return false;
        return content.EnumerateArray().Any(b => Str(b, "type") == "text");
    }

    internal static bool TryTime(JsonElement root, out DateTime local)
    {
        local = default;
        if (!root.TryGetProperty("timestamp", out var ts) || ts.ValueKind != JsonValueKind.String) return false;
        if (!DateTimeOffset.TryParse(ts.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var t)) return false;
        local = t.LocalDateTime;
        return true;
    }

    internal static string ProjectName(string cwd)
    {
        var trimmed = cwd.TrimEnd('\\', '/');
        var i = Math.Max(trimmed.LastIndexOf('\\'), trimmed.LastIndexOf('/'));
        return i >= 0 ? trimmed[(i + 1)..] : trimmed;
    }

    internal static string Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    internal static long Num(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n) ? n : 0;
}

/// <summary>Best-effort reader for OpenAI Codex CLI rollout logs: counts user messages and tool calls, and takes the
/// session's final cumulative token total.</summary>
public static class CodexLogParser
{
    public static IReadOnlyList<UsageEvent> ParseFile(IEnumerable<string> lines, string fallbackSession)
    {
        var events = new List<UsageEvent>();
        string session = fallbackSession, project = "", model = "gpt-5";
        long input = 0, output = 0, cached = 0;
        DateTime last = default;
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) continue;
                var hasTime = ClaudeLogParser.TryTime(root, out var time);
                if (hasTime) last = time;
                if (!root.TryGetProperty("payload", out var p) || p.ValueKind != JsonValueKind.Object) continue;
                var ptype = ClaudeLogParser.Str(p, "type");
                var rtype = ClaudeLogParser.Str(root, "type");

                if (rtype == "session_meta")
                {
                    if (ClaudeLogParser.Str(p, "id") is { Length: > 0 } id) session = id;
                    project = ClaudeLogParser.ProjectName(ClaudeLogParser.Str(p, "cwd"));
                }
                else if (rtype == "turn_context" && ClaudeLogParser.Str(p, "model") is { Length: > 0 } m) model = m;
                else if (rtype == "response_item" && hasTime)
                {
                    if (ptype == "message" && ClaudeLogParser.Str(p, "role") == "user")
                        events.Add(new UsageEvent(time, AiAgent.Codex, session, project, model, 0, 0, 0, 0, 1, 0));
                    else if (ptype is "function_call" or "local_shell_call" or "custom_tool_call")
                        events.Add(new UsageEvent(time, AiAgent.Codex, session, project, model, 0, 0, 0, 0, 0, 1));
                }
                else if (rtype == "event_msg" && ptype == "token_count" && p.TryGetProperty("info", out var info) &&
                         info.ValueKind == JsonValueKind.Object && info.TryGetProperty("total_token_usage", out var u))
                {
                    input = ClaudeLogParser.Num(u, "input_tokens");
                    cached = ClaudeLogParser.Num(u, "cached_input_tokens");
                    output = ClaudeLogParser.Num(u, "output_tokens");
                }
            }
            catch (JsonException) { }
        }
        if (input + output + cached > 0 && last != default)
            events.Add(new UsageEvent(last, AiAgent.Codex, session, project, model, Math.Max(0, input - cached), output, 0, cached, 0, 0));
        return events;
    }
}

public record DaySummary(long Tokens, double Cost, int Messages, int ToolCalls, int Sessions, int[] ActiveHours, string? TopProject);

public static class AiUsageSummary
{
    private static IEnumerable<UsageEvent> Filter(IEnumerable<UsageEvent> events, AiAgent? agent) =>
        agent is null ? events : events.Where(e => e.Agent == agent);

    public static DaySummary ForDay(IEnumerable<UsageEvent> events, DateTime day, AiAgent? agent)
    {
        var list = Filter(events, agent).Where(e => e.Time.Date == day.Date).ToList();
        var hours = new int[24];
        foreach (var e in list) hours[e.Time.Hour] = 1;
        var top = list.Where(e => e.Project.Length > 0).GroupBy(e => e.Project)
            .OrderByDescending(g => g.Sum(e => e.TotalTokens)).ThenByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault();
        return new DaySummary(list.Sum(e => e.TotalTokens), list.Sum(e => e.Cost), list.Sum(e => e.Messages), list.Sum(e => e.ToolCalls),
            list.Where(e => e.SessionId.Length > 0).Select(e => e.SessionId).Distinct().Count(), hours, top);
    }

    /// <summary>Consecutive days with any activity, counting back from today (0 when today has none).</summary>
    public static int Streak(IEnumerable<UsageEvent> events, DateTime today, AiAgent? agent)
    {
        var days = Filter(events, agent).Select(e => e.Time.Date).ToHashSet();
        var n = 0;
        for (var d = today.Date; days.Contains(d); d = d.AddDays(-1)) n++;
        return n;
    }

    /// <summary>"950", "12.4K", "3.2M", "1B".</summary>
    public static string FormatTokens(long tokens)
    {
        static string Trim(double v) => Math.Round(v, 1).ToString("0.#", CultureInfo.InvariantCulture);
        if (tokens < 1000) return tokens.ToString(CultureInfo.InvariantCulture);
        if (Math.Round(tokens / 1000.0, 1) < 1000) return Trim(tokens / 1000.0) + "K";
        if (Math.Round(tokens / 1_000_000.0, 1) < 1000) return Trim(tokens / 1_000_000.0) + "M";
        return Trim(tokens / 1_000_000_000.0) + "B";
    }

    public static double MonthCost(IEnumerable<UsageEvent> events, DateTime day, AiAgent? agent) =>
        Filter(events, agent).Where(e => e.Time.Year == day.Year && e.Time.Month == day.Month).Sum(e => e.Cost);
}
