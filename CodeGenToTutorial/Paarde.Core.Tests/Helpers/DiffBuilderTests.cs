using Paarde.Core.Helpers;
using Paarde.Core.Models;

namespace Paarde.Core.Tests.Helpers;

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

    [Fact]
    public void GroupIntoHunks_NoChanges_ReturnsNoHunks()
    {
        var diffLines = DiffBuilder.BuildLineDiff("line1\nline2", "line1\nline2");

        var hunks = DiffBuilder.GroupIntoHunks(diffLines);

        Assert.Empty(hunks);
    }

    [Fact]
    public void GroupIntoHunks_SingleChangeWithinContext_ProducesOneHunkWithFullContext()
    {
        // 3 unchanged lines, 1 changed line, 3 unchanged lines - the whole file fits inside one
        // hunk's context window (default 3 lines either side of the change).
        var original = "a\nb\nc\nOLD\nd\ne\nf";
        var proposed = "a\nb\nc\nNEW\nd\ne\nf";
        var diffLines = DiffBuilder.BuildLineDiff(original, proposed);

        var hunks = DiffBuilder.GroupIntoHunks(diffLines);

        var hunk = Assert.Single(hunks);
        Assert.Equal(diffLines, hunk.Lines);
        Assert.Equal("@@ -1,7 +1,7 @@", hunk.Header);
    }

    [Fact]
    public void GroupIntoHunks_ChangesFartherApartThanTwiceContext_ProducesSeparateHunks()
    {
        var unchangedGap = string.Join('\n', Enumerable.Range(0, 20).Select(i => $"gap-{i}"));
        var original = $"OLD1\n{unchangedGap}\nOLD2";
        var proposed = $"NEW1\n{unchangedGap}\nNEW2";
        var diffLines = DiffBuilder.BuildLineDiff(original, proposed);

        var hunks = DiffBuilder.GroupIntoHunks(diffLines, contextLines: 3);

        Assert.Equal(2, hunks.Count);
        Assert.Contains(hunks[0].Lines, l => l.Text == "OLD1");
        Assert.DoesNotContain(hunks[0].Lines, l => l.Text == "OLD2" || l.Text == "NEW2");
        Assert.Contains(hunks[1].Lines, l => l.Text == "OLD2");
        Assert.DoesNotContain(hunks[1].Lines, l => l.Text == "OLD1" || l.Text == "NEW1");
    }

    [Fact]
    public void GroupIntoHunks_ChangesWithinTwiceContext_MergeIntoOneHunk()
    {
        // Exactly 2 * contextLines unchanged lines between two single-line changes - close enough
        // that their context windows would overlap, so they merge into one hunk.
        var unchangedGap = string.Join('\n', Enumerable.Range(0, 6).Select(i => $"gap-{i}"));
        var original = $"OLD1\n{unchangedGap}\nOLD2";
        var proposed = $"NEW1\n{unchangedGap}\nNEW2";
        var diffLines = DiffBuilder.BuildLineDiff(original, proposed);

        var hunks = DiffBuilder.GroupIntoHunks(diffLines, contextLines: 3);

        var hunk = Assert.Single(hunks);
        Assert.Contains(hunk.Lines, l => l.Text == "OLD1");
        Assert.Contains(hunk.Lines, l => l.Text == "OLD2");
    }

    [Fact]
    public void GroupIntoHunks_PureInsertionIntoEmptyFile_ReportsZeroOriginalCount()
    {
        var diffLines = DiffBuilder.BuildLineDiff(string.Empty, "line1\nline2");

        var hunk = Assert.Single(DiffBuilder.GroupIntoHunks(diffLines));

        Assert.Equal("@@ -0,0 +1,2 @@", hunk.Header);
    }

    [Fact]
    public void GroupIntoHunks_ChangeNearStartOfFile_ClampsContextToFileBounds()
    {
        var original = "OLD\nb\nc\nd";
        var proposed = "NEW\nb\nc\nd";
        var diffLines = DiffBuilder.BuildLineDiff(original, proposed);

        var hunk = Assert.Single(DiffBuilder.GroupIntoHunks(diffLines, contextLines: 3));

        Assert.Equal(diffLines, hunk.Lines);
        Assert.Equal("@@ -1,4 +1,4 @@", hunk.Header);
    }

    [Fact]
    public void ComposeContent_AllHunksSelected_ReproducesProposedContent()
    {
        var original = "a\nOLD1\nb\nOLD2\nc";
        var proposed = "a\nNEW1\nb\nNEW2\nc";
        var diffLines = DiffBuilder.BuildLineDiff(original, proposed);
        var hunks = DiffBuilder.GroupIntoHunks(diffLines, contextLines: 1);

        var result = DiffBuilder.ComposeContent(diffLines, hunks, _ => true);

        Assert.Equal(proposed, result);
    }

    [Fact]
    public void ComposeContent_NoHunksSelected_ReproducesOriginalContent()
    {
        var original = "a\nOLD1\nb\nOLD2\nc";
        var proposed = "a\nNEW1\nb\nNEW2\nc";
        var diffLines = DiffBuilder.BuildLineDiff(original, proposed);
        var hunks = DiffBuilder.GroupIntoHunks(diffLines, contextLines: 1);

        var result = DiffBuilder.ComposeContent(diffLines, hunks, _ => false);

        Assert.Equal(original, result);
    }

    [Fact]
    public void ComposeContent_MixedSelection_AppliesOnlySelectedHunks()
    {
        // Two single-line changes far enough apart that GroupIntoHunks keeps them as separate hunks.
        var unchangedGap = string.Join('\n', Enumerable.Range(0, 20).Select(i => $"gap-{i}"));
        var original = $"OLD1\n{unchangedGap}\nOLD2";
        var proposed = $"NEW1\n{unchangedGap}\nNEW2";
        var diffLines = DiffBuilder.BuildLineDiff(original, proposed);
        var hunks = DiffBuilder.GroupIntoHunks(diffLines, contextLines: 3);
        Assert.Equal(2, hunks.Count);

        var result = DiffBuilder.ComposeContent(diffLines, hunks, hunk => hunk == hunks[0]);

        Assert.Equal($"NEW1\n{unchangedGap}\nOLD2", result);
    }

    [Fact]
    public void ComposeContent_NoChanges_ReturnsOriginalText()
    {
        const string text = "line1\nline2\nline3";
        var diffLines = DiffBuilder.BuildLineDiff(text, text);
        var hunks = DiffBuilder.GroupIntoHunks(diffLines);

        var result = DiffBuilder.ComposeContent(diffLines, hunks, _ => true);

        Assert.Equal(text, result);
    }

    [Fact]
    public void ComposeContent_PureInsertionSelected_IncludesInsertedLines()
    {
        var diffLines = DiffBuilder.BuildLineDiff(string.Empty, "line1\nline2");
        var hunks = DiffBuilder.GroupIntoHunks(diffLines);

        var result = DiffBuilder.ComposeContent(diffLines, hunks, _ => true);

        Assert.Equal("line1\nline2", result);
    }

    [Fact]
    public void ComposeContent_PureInsertionNotSelected_ReturnsEmpty()
    {
        var diffLines = DiffBuilder.BuildLineDiff(string.Empty, "line1\nline2");
        var hunks = DiffBuilder.GroupIntoHunks(diffLines);

        var result = DiffBuilder.ComposeContent(diffLines, hunks, _ => false);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ComposeContent_PureDeletionSelected_RemovesLines()
    {
        var diffLines = DiffBuilder.BuildLineDiff("line1\nline2", string.Empty);
        var hunks = DiffBuilder.GroupIntoHunks(diffLines);

        var result = DiffBuilder.ComposeContent(diffLines, hunks, _ => true);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ComposeContent_PureDeletionNotSelected_KeepsOriginalLines()
    {
        var diffLines = DiffBuilder.BuildLineDiff("line1\nline2", string.Empty);
        var hunks = DiffBuilder.GroupIntoHunks(diffLines);

        var result = DiffBuilder.ComposeContent(diffLines, hunks, _ => false);

        Assert.Equal("line1\nline2", result);
    }
}
