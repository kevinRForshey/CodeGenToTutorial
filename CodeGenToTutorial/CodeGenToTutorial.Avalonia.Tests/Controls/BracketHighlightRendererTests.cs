using CodeGenToTutorial.Avalonia.Controls;

namespace CodeGenToTutorial.Avalonia.Tests.Controls;

public class BracketHighlightRendererTests
{
    [Fact]
    public void FindMatchingPair_CaretAfterOpenParen_ReturnsOpenAndCloseOffsets()
    {
        const string text = "foo(bar)";

        var result = BracketHighlightRenderer.FindMatchingPair(text, 4);

        Assert.Equal((3, 7), result);
    }

    [Fact]
    public void FindMatchingPair_CaretAfterCloseParen_ReturnsOpenAndCloseOffsets()
    {
        const string text = "foo(bar)";

        var result = BracketHighlightRenderer.FindMatchingPair(text, 8);

        Assert.Equal((3, 7), result);
    }

    [Fact]
    public void FindMatchingPair_NestedBrackets_SkipsInnerPairToFindOuterMatch()
    {
        const string text = "{a[b]c}";

        var result = BracketHighlightRenderer.FindMatchingPair(text, 1);

        Assert.Equal((0, 6), result);
    }

    [Fact]
    public void FindMatchingPair_UnmatchedBracket_ReturnsNoMatch()
    {
        const string text = "foo(bar";

        var result = BracketHighlightRenderer.FindMatchingPair(text, 4);

        Assert.Equal((-1, -1), result);
    }

    [Fact]
    public void FindMatchingPair_CaretNotNearBracket_ReturnsNoMatch()
    {
        const string text = "foo(bar)";

        var result = BracketHighlightRenderer.FindMatchingPair(text, 1);

        Assert.Equal((-1, -1), result);
    }
}
