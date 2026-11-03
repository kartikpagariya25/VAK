using System.Text;

namespace VAK.Core.AiUsage;

/// <summary>Scans local Claude Code and Codex logs (read-only) and returns usage events for the last few weeks.
/// Each log file is parsed once and cached by (path, length, modified time), so a running session only re-reads the file it is
/// writing, not every log. Files are opened with sharing so a session can keep writing, and a message or prompt that appears in
/// two files (a resumed session repeats its history) is counted once.</summary>
public class AiUsageCollector
{
    private readonly string _claudeDir;
    private readonly string _codexDir;
    private readonly Func<DateTime> _now;
    private readonly Dictionary<string, (long Length, long Ticks, List<UsageEvent> Events)> _cache = new();

    public AiUsageCollector(string claudeProjectsDir, string codexSessionsDir, Func<DateTime> now)
    {
        _claudeDir = claudeProjectsDir; _codexDir = codexSessionsDir; _now = now;
    }

    public static AiUsageCollector ForCurrentUser(Func<DateTime> now)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var claude = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR") is { Length: > 0 } cfg
            ? Path.Combine(cfg, "projects") : Path.Combine(home, ".claude", "projects");
        return new AiUsageCollector(claude, Path.Combine(home, ".codex", "sessions"), now);
    }

    /// <summary>Events from files changed in the last <paramref name="days"/> days.</summary>
    public IReadOnlyList<UsageEvent> Collect(int days = 35)
    {
        var cutoff = _now().AddDays(-days);
        var claudeFiles = Files(_claudeDir, cutoff);
        var codexFiles = Files(_codexDir, cutoff);

        var live = new HashSet<string>(claudeFiles.Concat(codexFiles).Select(f => f.FullName));
        foreach (var gone in _cache.Keys.Where(k => !live.Contains(k)).ToList()) _cache.Remove(gone);

        var events = new List<UsageEvent>();
        var seen = new HashSet<string>();
        foreach (var f in claudeFiles.OrderBy(f => f.LastWriteTimeUtc))
            foreach (var e in Cached(f, () => ParseClaude(f)))
                if (e.Time >= cutoff && (e.Id.Length == 0 || seen.Add(e.Id))) events.Add(e);
        foreach (var f in codexFiles)
            events.AddRange(Cached(f, () => CodexLogParser.ParseFile(Lines(f), Path.GetFileNameWithoutExtension(f.Name)).ToList()).Where(e => e.Time >= cutoff));
        return events;
    }

    private List<UsageEvent> Cached(FileInfo file, Func<List<UsageEvent>> parse)
    {
        if (_cache.TryGetValue(file.FullName, out var hit) && hit.Length == file.Length && hit.Ticks == file.LastWriteTimeUtc.Ticks) return hit.Events;
        var events = parse();
        _cache[file.FullName] = (file.Length, file.LastWriteTimeUtc.Ticks, events);
        return events;
    }

    private static List<UsageEvent> ParseClaude(FileInfo file)
    {
        var parser = new ClaudeLogParser(); // one per file: repeats inside a file are dropped here, repeats across files in Collect
        var events = new List<UsageEvent>();
        foreach (var line in Lines(file))
            if (parser.ParseLine(line) is { } e) events.Add(e);
        return events;
    }

    private static List<FileInfo> Files(string dir, DateTime cutoff)
    {
        try
        {
            if (!Directory.Exists(dir)) return new List<FileInfo>();
            return new DirectoryInfo(dir).EnumerateFiles("*.jsonl", SearchOption.AllDirectories).Where(f => f.LastWriteTime >= cutoff).ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return new List<FileInfo>(); }
    }

    private static IEnumerable<string> Lines(FileInfo file)
    {
        FileStream stream;
        try { stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { yield break; }
        using (stream)
        using (var reader = new StreamReader(stream, Encoding.UTF8))
        {
            string? line;
            while ((line = reader.ReadLine()) is not null) yield return line;
        }
    }
}
