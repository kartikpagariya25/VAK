using System.Text;

namespace VAK.Core.Chat;

public enum MdKind { Paragraph, Heading, Bullet, Numbered, Code }

public record MdBlock(MdKind Kind, string Text, string Language = "", int Level = 0);

public record InlineRun(string Text, bool Bold = false, bool Italic = false, bool Code = false);

/// <summary>A small markdown reader for chat replies: headings, paragraphs, bullet and numbered lists, fenced code, and **bold**, *italic*, `code`.
/// An unclosed code fence counts as code, so a reply that is still streaming renders sensibly.</summary>
public static class MarkdownParser
{
    public static IEnumerable<MdBlock> Parse(string text)
    {
        var lines = (text ?? "").Replace("\r\n", "\n").Split('\n');
        var paragraph = new List<string>();
        var blocks = new List<MdBlock>();

        void Flush()
        {
            if (paragraph.Count > 0) blocks.Add(new MdBlock(MdKind.Paragraph, string.Join("\n", paragraph)));
            paragraph.Clear();
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("```"))
            {
                Flush();
                var language = trimmed[3..].Trim();
                var code = new List<string>();
                for (i++; i < lines.Length && !lines[i].TrimStart().StartsWith("```"); i++) code.Add(lines[i]);
                blocks.Add(new MdBlock(MdKind.Code, string.Join("\n", code), language));
                continue;
            }
            if (trimmed.Length == 0) { Flush(); continue; }

            var hashes = trimmed.TakeWhile(c => c == '#').Count();
            if (hashes is >= 1 and <= 6 && trimmed.Length > hashes && trimmed[hashes] == ' ')
            {
                Flush();
                blocks.Add(new MdBlock(MdKind.Heading, trimmed[(hashes + 1)..].Trim(), Level: hashes));
            }
            else if (trimmed.StartsWith("- ") || trimmed.StartsWith("* "))
            {
                Flush();
                blocks.Add(new MdBlock(MdKind.Bullet, trimmed[2..].Trim()));
            }
            else if (IsNumbered(trimmed))
            {
                Flush();
                blocks.Add(new MdBlock(MdKind.Numbered, trimmed));
            }
            else paragraph.Add(line.Trim());
        }
        Flush();
        return blocks;
    }

    private static bool IsNumbered(string s)
    {
        var digits = s.TakeWhile(char.IsDigit).Count();
        return digits is >= 1 and <= 3 && s.Length > digits + 1 && s[digits] == '.' && s[digits + 1] == ' ';
    }

    public static IEnumerable<InlineRun> Inline(string text)
    {
        var runs = new List<InlineRun>();
        var plain = new StringBuilder();
        void FlushPlain() { if (plain.Length > 0) runs.Add(new InlineRun(plain.ToString())); plain.Clear(); }

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '`')
            {
                var end = text.IndexOf('`', i + 1);
                if (end > i + 1) { FlushPlain(); runs.Add(new InlineRun(text[(i + 1)..end], Code: true)); i = end; continue; }
            }
            else if (c == '*' && i + 1 < text.Length && text[i + 1] == '*')
            {
                var end = text.IndexOf("**", i + 2, StringComparison.Ordinal);
                if (end > i + 2) { FlushPlain(); runs.Add(new InlineRun(text[(i + 2)..end], Bold: true)); i = end + 1; continue; }
            }
            else if (c == '*' && i + 1 < text.Length && text[i + 1] != ' ')
            {
                var end = text.IndexOf('*', i + 1);
                if (end > i + 1 && text[end - 1] != ' ') { FlushPlain(); runs.Add(new InlineRun(text[(i + 1)..end], Italic: true)); i = end; continue; }
            }
            plain.Append(c);
        }
        FlushPlain();
        return runs;
    }
}
