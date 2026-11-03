using System.Text;

namespace VAK.Core.AiUsage;

/// <summary>Scans local Claude Code and Codex logs (read-only) and returns usage events for the last few weeks.
/// Files are opened with sharing so a running session can keep writing, and a message that appears in two files
/// (a resumed session repeats its history) is counted once.</summary>
public class AiUsageCollector
{
    private readonly string _claudeDir;
    private readonly string _codexDir;
    private readonly Func<DateTime> _now;
    private string _signature = "";
    private IReadOnlyList<UsageEvent> _cached = Array.Empty<UsageEvent>();

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

    /// <summary>Events from files changed in the last <paramref name="days"/> days. Unchanged logs return the previous result.</summary>
    public IReadOnlyList<UsageEvent> Collect(int days = 35)
    {
        var cutoff = _now().AddDays(-days);
        var claudeFiles = Files(_claudeDir, cutoff);
        var codexFiles = Files(_codexDir, cutoff);
        var signature = string.Join("|", claudeFiles.Concat(codexFiles).Select(f => $"{f.FullName}:{f.Length}:{f.LastWriteTimeUtc.Ticks}"));
        if (signature == _signature) return _cached;

        var events = new List<UsageEvent>();
        var parser = new ClaudeLogParser(); // shared, so a repeated message id is ignored across files
        foreach (var f in claudeFiles.OrderBy(f => f.LastWriteTimeUtc))
            foreach (var line in Lines(f))
                if (parser.ParseLine(line) is { } e && e.Time >= cutoff) events.Add(e);
        foreach (var f in codexFiles)
            events.AddRange(CodexLogParser.ParseFile(Lines(f), Path.GetFileNameWithoutExtension(f.Name)).Where(e => e.Time >= cutoff));

        _signature = signature;
        return _cached = events;
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
