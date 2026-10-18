using Paarde.Core.Helpers;
using Paarde.Core.Models;

namespace Paarde.Core.Tests.Helpers;

public class MarkdownInlineParserTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n  ")]
    public void Parse_EmptyOrWhitespace_ReturnsNoLines(string? text)
    {
        Assert.Empty(MarkdownInlineParser.Parse(text));
    }

    [Fact]
    public void Parse_PlainText_ReturnsSingleTextSpan()
    {
        var lines = MarkdownInlineParser.Parse("just plain text");

        var line = Assert.Single(lines);
        Assert.False(line.IsListItem);
        var span = Assert.Single(line.Spans);
        Assert.Equal(MarkdownSpanKind.Text, span.Kind);
        Assert.Equal("just plain text", span.Text);
    }

    [Fact]
    public void Parse_MultipleLines_SkipsBlankLines()
    {
        var lines = MarkdownInlineParser.Parse("first\n\nsecond\r\n\r\nthird");

        Assert.Equal(["first", "second", "third"], lines.Select(l => Assert.Single(l.Spans).Text));
    }

    [Theory]
    [InlineData("- a bullet item")]
    [InlineData("* a bullet item")]
    [InlineData("+ a bullet item")]
    [InlineData("1. a bullet item")]
    [InlineData("42. a bullet item")]
    public void Parse_ListMarker_SetsIsListItemAndStripsMarker(string line)
    {
        var parsed = Assert.Single(MarkdownInlineParser.Parse(line));

        Assert.True(parsed.IsListItem);
        Assert.Equal("a bullet item", Assert.Single(parsed.Spans).Text);
    }

    [Fact]
    public void Parse_Bold_ExtractsBoldSpanWithoutMarkers()
    {
        var line = Assert.Single(MarkdownInlineParser.Parse("do **this** now"));

        Assert.Equal(
            [(MarkdownSpanKind.Text, "do "), (MarkdownSpanKind.Bold, "this"), (MarkdownSpanKind.Text, " now")],
            line.Spans.Select(s => (s.Kind, s.Text)));
    }

    [Theory]
    [InlineData("do *this* now")]
    [InlineData("do _this_ now")]
    public void Parse_Italic_ExtractsItalicSpanWithoutMarkers(string text)
    {
        var line = Assert.Single(MarkdownInlineParser.Parse(text));

        Assert.Equal(
            [(MarkdownSpanKind.Text, "do "), (MarkdownSpanKind.Italic, "this"), (MarkdownSpanKind.Text, " now")],
            line.Spans.Select(s => (s.Kind, s.Text)));
    }

    [Fact]
    public void Parse_CodeSpan_ExtractsCodeSpanWithoutBackticks()
    {
        var line = Assert.Single(MarkdownInlineParser.Parse("call `DoThing()` here"));

        Assert.Equal(
            [(MarkdownSpanKind.Text, "call "), (MarkdownSpanKind.Code, "DoThing()"), (MarkdownSpanKind.Text, " here")],
            line.Spans.Select(s => (s.Kind, s.Text)));
    }

    [Fact]
    public void Parse_MixedEmphasisAndCode_ProducesSpansInOrder()
    {
        var line = Assert.Single(MarkdownInlineParser.Parse("**bold** then *italic* then `code`"));

        Assert.Equal(
            [
                (MarkdownSpanKind.Bold, "bold"),
                (MarkdownSpanKind.Text, " then "),
                (MarkdownSpanKind.Italic, "italic"),
                (MarkdownSpanKind.Text, " then "),
                (MarkdownSpanKind.Code, "code"),
            ],
            line.Spans.Select(s => (s.Kind, s.Text)));
    }

    [Fact]
    public void Parse_ListItemWithEmphasis_StripsMarkerAndParsesSpans()
    {
        var line = Assert.Single(MarkdownInlineParser.Parse("- use `Foo` for **bar**"));

        Assert.True(line.IsListItem);
        Assert.Equal(
            [(MarkdownSpanKind.Text, "use "), (MarkdownSpanKind.Code, "Foo"), (MarkdownSpanKind.Text, " for "), (MarkdownSpanKind.Bold, "bar")],
            line.Spans.Select(s => (s.Kind, s.Text)));
    }
}
