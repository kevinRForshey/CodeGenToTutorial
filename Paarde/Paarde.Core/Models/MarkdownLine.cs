namespace Paarde.Core.Models;

public enum MarkdownSpanKind
{
    Text,
    Bold,
    Italic,
    Code,
}

public record MarkdownSpan(MarkdownSpanKind Kind, string Text);

public record MarkdownLine(bool IsListItem, IReadOnlyList<MarkdownSpan> Spans);
