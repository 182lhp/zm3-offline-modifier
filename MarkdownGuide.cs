using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace GmStudio;

internal static class MarkdownGuide
{
    private static readonly Regex Inline = new(@"(`[^`]+`|\*\*[^*]+\*\*|\[[^\]]+\]\([^)]+\))", RegexOptions.Compiled);
    private static readonly Brush Ink = new SolidColorBrush(Color.FromRgb(24, 58, 51));
    private static readonly Brush Accent = new SolidColorBrush(Color.FromRgb(22, 108, 88));
    private static readonly Brush Soft = new SolidColorBrush(Color.FromRgb(245, 240, 228));

    internal static FlowDocument Render(string markdown, Action<string> follow)
    {
        var document = new FlowDocument
        {
            FontSize = 13, Foreground = Ink, PagePadding = new Thickness(18),
            ColumnWidth = double.PositiveInfinity, LineHeight = 23
        };
        var paragraph = new List<string>();
        var code = new List<string>();
        bool inCode = false;
        void Flush()
        {
            if (paragraph.Count == 0) return;
            var block = new Paragraph { Margin = new Thickness(0, 0, 0, 10) };
            AddInline(block, string.Join(" ", paragraph), follow);
            document.Blocks.Add(block);
            paragraph.Clear();
        }
        void FlushCode()
        {
            document.Blocks.Add(new Paragraph(new Run(string.Join("\n", code)))
            {
                FontFamily = new FontFamily("Consolas, Microsoft YaHei UI"), FontSize = 12,
                Background = Soft, Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 3, 0, 12)
            });
            code.Clear();
        }
        foreach (string raw in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            string line = raw.TrimEnd();
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                Flush();
                if (inCode) FlushCode();
                inCode = !inCode;
                continue;
            }
            if (inCode) { code.Add(line); continue; }
            if (string.IsNullOrWhiteSpace(line)) { Flush(); continue; }
            var heading = Regex.Match(line, @"^(#{1,6})\s+(.+)$");
            if (heading.Success)
            {
                Flush();
                int level = heading.Groups[1].Length;
                var block = new Paragraph
                {
                    FontSize = level == 1 ? 24 : level == 2 ? 18 : 15,
                    FontWeight = FontWeights.Bold, Foreground = level == 1 ? Ink : Accent,
                    Margin = new Thickness(0, document.Blocks.Count == 0 ? 0 : 15, 0, 9), KeepWithNext = true
                };
                AddInline(block, heading.Groups[2].Value, follow);
                document.Blocks.Add(block);
                continue;
            }
            var item = Regex.Match(line, @"^\s*(?:([-*+])\s+|(\d+)\.\s+)(.+)$");
            if (item.Success)
            {
                Flush();
                var block = new Paragraph { Margin = new Thickness(9, 0, 0, 7) };
                block.Inlines.Add(new Run(item.Groups[2].Success ? item.Groups[2].Value + ".  " : "•  ")
                    { Foreground = Accent, FontWeight = FontWeights.Bold });
                AddInline(block, item.Groups[3].Value, follow);
                document.Blocks.Add(block);
                continue;
            }
            paragraph.Add(line.Trim());
        }
        Flush();
        if (code.Count > 0) FlushCode();
        return document;
    }

    private static void AddInline(Paragraph block, string text, Action<string> follow)
    {
        int position = 0;
        foreach (Match match in Inline.Matches(text))
        {
            if (match.Index > position) block.Inlines.Add(new Run(text[position..match.Index]));
            string token = match.Value;
            if (token.StartsWith('`'))
                block.Inlines.Add(new Run(token[1..^1]) { Background = Soft, FontFamily = new FontFamily("Consolas, Microsoft YaHei UI") });
            else if (token.StartsWith("**", StringComparison.Ordinal))
                block.Inlines.Add(new Bold(new Run(token[2..^2])));
            else
            {
                int delimiter = token.IndexOf("](", StringComparison.Ordinal);
                string target = token[(delimiter+2)..^1];
                var link = new Hyperlink(new Run(token[1..delimiter])) { Foreground = Accent };
                link.Click += (_, _) => follow(target);
                block.Inlines.Add(link);
            }
            position = match.Index + match.Length;
        }
        if (position < text.Length) block.Inlines.Add(new Run(text[position..]));
    }
}
