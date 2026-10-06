using CodeGenToTutorial.Core.Helpers;
using CodeGenToTutorial.Core.Models;

namespace CodeGenToTutorial.Core.Tests.Helpers;

public class DiffBuilderTests
{
    [Fact]
    public void BuildLineDiff_BothEmpty_ReturnsNoLines()
    {
        var result = DiffBuilder.BuildLineDiff(string.Empty, string.Empty);

        Assert.Empty(result);
    }

    [Fact]
    public void BuildLineDiff_IdenticalText_ReturnsAllUnchanged()
    {
        const string text = "line1\nline2\nline3";

        var result = DiffBuilder.BuildLineDiff(text, text);

        Assert.Equal(3, result.Count);
        Assert.All(result, line => Assert.Equal(DiffLineKind.Unchanged, line.Kind));
    }

    [Fact]
    public void BuildLineDiff_LineInserted_MarksOnlyNewLineAsAdded()
    {
        var original = "line1\nline3";
        var proposed = "line1\nline2\nline3";

        var result = DiffBuilder.BuildLineDiff(original, proposed);

        Assert.Equal(
            [
                (DiffLineKind.Unchanged, "line1"),
                (DiffLineKind.Added, "line2"),
                (DiffLineKind.Unchanged, "line3"),
            ],
            result.Select(l => (l.Kind, l.Text)));
    }

    [Fact]
    public void BuildLineDiff_LineDeleted_MarksOnlyRemovedLineAsRemoved()
    {
        var original = "line1\nline2\nline3";
        var proposed = "line1\nline3";

        var result = DiffBuilder.BuildLineDiff(original, proposed);

        Assert.Equal(
            [
                (DiffLineKind.Unchanged, "line1"),
                (DiffLineKind.Removed, "line2"),
                (DiffLineKind.Unchanged, "line3"),
            ],
            result.Select(l => (l.Kind, l.Text)));
    }

    [Fact]
    public void BuildLineDiff_LineMoved_RepresentsAsRemoveThenAdd()
    {
        var original = "a\nb\nc";
        var proposed = "b\nc\na";

        var result = DiffBuilder.BuildLineDiff(original, proposed);

        // "a" moving to the end has no shared ordering with the rest, so the shortest edit script
        // removes it from the front and adds it back at the end rather than reordering in place.
        Assert.Equal(
            [
                (DiffLineKind.Removed, "a"),
                (DiffLineKind.Unchanged, "b"),
                (DiffLineKind.Unchanged, "c"),
                (DiffLineKind.Added, "a"),
            ],
            result.Select(l => (l.Kind, l.Text)));
    }

    [Fact]
    public void BuildLineDiff_CrlfVsLf_TreatsEquivalentLinesAsUnchanged()
    {
        var original = "line1\r\nline2\r\nline3";
        var proposed = "line1\nline2\nline3";

        var result = DiffBuilder.BuildLineDiff(original, proposed);

        Assert.All(result, line => Assert.Equal(DiffLineKind.Unchanged, line.Kind));
    }

    [Fact]
    public void BuildLineDiff_OriginalEmptyProposedHasContent_AllLinesAdded()
    {
        var result = DiffBuilder.BuildLineDiff(string.Empty, "line1\nline2");

        Assert.Equal(2, result.Count);
        Assert.All(result, line => Assert.Equal(DiffLineKind.Added, line.Kind));
    }

    [Fact]
    public void BuildLineDiff_ProposedEmptyOriginalHasContent_AllLinesRemoved()
    {
        var result = DiffBuilder.BuildLineDiff("line1\nline2", string.Empty);

        Assert.Equal(2, result.Count);
        Assert.All(result, line => Assert.Equal(DiffLineKind.Removed, line.Kind));
    }

    [Fact]
    public void BuildLineDiff_ExceedsMaxLcsCells_FallsBackToWholeFileReplace()
    {
        // MaxLcsCells is 4_000_000; 2001 * 2001 > that, so the LCS table is skipped entirely and the
        // whole file is treated as replaced (all original lines removed, all proposed lines added, with
        // no attempt to match up common lines).
        var original = string.Join('\n', Enumerable.Range(0, 2001).Select(i => $"same-line-{i}"));
        var proposed = string.Join('\n', Enumerable.Range(0, 2001).Select(i => $"same-line-{i}"));

        var result = DiffBuilder.BuildLineDiff(original, proposed);

        Assert.Equal(4002, result.Count);
        Assert.Equal(2001, result.Count(l => l.Kind == DiffLineKind.Removed));
        Assert.Equal(2001, result.Count(l => l.Kind == DiffLineKind.Added));
        Assert.DoesNotContain(result, l => l.Kind == DiffLineKind.Unchanged);
    }
}
