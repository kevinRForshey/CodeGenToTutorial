using System.Text.RegularExpressions;

using Paarde.Core.Models;

namespace Paarde.Core.Helpers;

// Renders the limited markdown subset the LLM's tutorial/explanation text actually uses - bullet
// and numbered lists, bold/italic emphasis, and inline code spans - without pulling in a full
// markdown-rendering dependency. Block constructs beyond single lines (headings, fenced code
// blocks, tables, nested lists) are intentionally out of scope; see T8.4.2.
public static partial class MarkdownInlineParser
{
    public static IReadOnlyList<MarkdownLine> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return text.Replace("\r\n", "\n")
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .Select(ParseLine)
            .ToList();
    }

    private static MarkdownLine ParseLine(string line)
    {
        var listMatch = ListMarkerRegex().Match(line);
        var content = listMatch.Success ? listMatch.Groups["content"].Value : line;

        return new MarkdownLine(listMatch.Success, ParseSpans(content));
    }

    private static IReadOnlyList<MarkdownSpan> ParseSpans(string content)
    {
        var spans = new List<MarkdownSpan>();
        var lastIndex = 0;

        foreach (Match match in InlineTokenRegex().Matches(content))
        {
            if (match.Index > lastIndex)
            {
                spans.Add(new MarkdownSpan(MarkdownSpanKind.Text, content[lastIndex..match.Index]));
            }

            spans.Add(match.Groups["code"].Success
                ? new MarkdownSpan(MarkdownSpanKind.Code, match.Groups["code"].Value[1..^1])
                : match.Groups["bold"].Success
                    ? new MarkdownSpan(MarkdownSpanKind.Bold, match.Groups["bold"].Value[2..^2])
                    : new MarkdownSpan(MarkdownSpanKind.Italic, match.Groups["italic"].Value[1..^1]));

            lastIndex = match.Index + match.Length;
        }

        if (lastIndex < content.Length)
        {
            spans.Add(new MarkdownSpan(MarkdownSpanKind.Text, content[lastIndex..]));
        }

        return spans;
    }

    [GeneratedRegex(@"^(?:[-*+]|\d+\.)\s+(?<content>.*)$")]
    private static partial Regex ListMarkerRegex();

    [GeneratedRegex(@"(?<code>`[^`]+`)|(?<bold>\*\*[^*]+\*\*|__[^_]+__)|(?<italic>\*[^*\n]+\*|_[^_\n]+_)")]
    private static partial Regex InlineTokenRegex();
}
