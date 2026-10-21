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

    [Fact]
    public void BuildSideBySideRows_NoChanges_PairsEachUnchangedLineWithItself()
    {
        var diffLines = DiffBuilder.BuildLineDiff("line1\nline2", "line1\nline2");

        var rows = DiffBuilder.BuildSideBySideRows(diffLines);

        Assert.Equal(
            [
                ("line1", DiffLineKind.Unchanged, "line1", DiffLineKind.Unchanged),
                ("line2", DiffLineKind.Unchanged, "line2", DiffLineKind.Unchanged),
            ],
            rows.Select(r => (r.OriginalText, r.OriginalKind, r.ProposedText, r.ProposedKind)));
    }

    [Fact]
    public void BuildSideBySideRows_SameCountOfRemovedAndAdded_PairsThemOneToOne()
    {
        var diffLines = DiffBuilder.BuildLineDiff("a\nb", "x\ny");

        var rows = DiffBuilder.BuildSideBySideRows(diffLines);

        Assert.Equal(
            [
                ("a", DiffLineKind.Removed, "x", DiffLineKind.Added),
                ("b", DiffLineKind.Removed, "y", DiffLineKind.Added),
            ],
            rows.Select(r => (r.OriginalText, r.OriginalKind, r.ProposedText, r.ProposedKind)));
    }

    [Fact]
    public void BuildSideBySideRows_MoreAddedThanRemoved_PadsOriginalSideWithBlankRows()
    {
        var diffLines = DiffBuilder.BuildLineDiff("a", "x\ny\nz");

        var rows = DiffBuilder.BuildSideBySideRows(diffLines);

        Assert.Equal(
            [
                ("a", DiffLineKind.Removed, "x", DiffLineKind.Added),
                (null, null, "y", DiffLineKind.Added),
                (null, null, "z", DiffLineKind.Added),
            ],
            rows.Select(r => (r.OriginalText, r.OriginalKind, r.ProposedText, r.ProposedKind)));
    }

    [Fact]
    public void BuildSideBySideRows_MoreRemovedThanAdded_PadsProposedSideWithBlankRows()
    {
        var diffLines = DiffBuilder.BuildLineDiff("x\ny\nz", "a");

        var rows = DiffBuilder.BuildSideBySideRows(diffLines);

        Assert.Equal(
            [
                ("x", DiffLineKind.Removed, "a", DiffLineKind.Added),
                ("y", DiffLineKind.Removed, null, null),
                ("z", DiffLineKind.Removed, null, null),
            ],
            rows.Select(r => (r.OriginalText, r.OriginalKind, r.ProposedText, r.ProposedKind)));
    }

    [Fact]
    public void BuildSideBySideRows_ChangeBetweenUnchangedLines_KeepsUnchangedLinesOnTheirOwnRows()
    {
        var diffLines = DiffBuilder.BuildLineDiff("line1\nold\nline3", "line1\nnew\nline3");

        var rows = DiffBuilder.BuildSideBySideRows(diffLines);

        Assert.Equal(
            [
                ("line1", DiffLineKind.Unchanged, "line1", DiffLineKind.Unchanged),
                ("old", DiffLineKind.Removed, "new", DiffLineKind.Added),
                ("line3", DiffLineKind.Unchanged, "line3", DiffLineKind.Unchanged),
            ],
            rows.Select(r => (r.OriginalText, r.OriginalKind, r.ProposedText, r.ProposedKind)));
    }

    [Fact]
    public void BuildSideBySideRows_Empty_ReturnsNoRows()
    {
        var rows = DiffBuilder.BuildSideBySideRows(Array.Empty<DiffLine>());

        Assert.Empty(rows);
    }

    [Fact]
    public void SideBySideDiffRow_Prefixes_AreBlankOnlyForPaddedSide()
    {
        var diffLines = DiffBuilder.BuildLineDiff("a", "x\ny");
        var rows = DiffBuilder.BuildSideBySideRows(diffLines);

        Assert.Equal(("- ", "+ "), (rows[0].OriginalPrefix, rows[0].ProposedPrefix));
        Assert.Equal(("", "+ "), (rows[1].OriginalPrefix, rows[1].ProposedPrefix));
    }

    [Fact]
    public void BuildCollapsibleRows_ShortUnchangedRun_NothingCollapses()
    {
        // 3 unchanged lines, 1 change, 3 unchanged lines - every unchanged run fits entirely within
        // contextLines of the single change, so nothing is far enough away to collapse.
        var original = "a\nb\nc\nOLD\nd\ne\nf";
        var proposed = "a\nb\nc\nNEW\nd\ne\nf";
        var diffLines = DiffBuilder.BuildLineDiff(original, proposed);

        var rows = DiffBuilder.BuildCollapsibleRows(diffLines, new HashSet<int>());

        Assert.Equal(diffLines.Count, rows.Count);
        Assert.DoesNotContain(rows, r => r.IsCollapsedPlaceholder);
        Assert.Equal(diffLines, rows.Select(r => r.Line));
    }

    [Fact]
    public void BuildCollapsibleRows_LongUnchangedRunBetweenChanges_CollapsesTheMiddleKeepingContext()
    {
        var unchangedGap = string.Join('\n', Enumerable.Range(0, 20).Select(i => $"gap-{i}"));
        var original = $"OLD1\n{unchangedGap}\nOLD2";
        var proposed = $"NEW1\n{unchangedGap}\nNEW2";
        var diffLines = DiffBuilder.BuildLineDiff(original, proposed);

        var rows = DiffBuilder.BuildCollapsibleRows(diffLines, new HashSet<int>(), contextLines: 3);

        var placeholder = Assert.Single(rows, r => r.IsCollapsedPlaceholder);
        Assert.Equal(20 - 3 - 3, placeholder.CollapsedLineCount);
        Assert.Contains(rows, r => r.Line?.Text == "gap-0");
        Assert.Contains(rows, r => r.Line?.Text == "gap-2");
        Assert.DoesNotContain(rows, r => r.Line?.Text == "gap-3");
        Assert.DoesNotContain(rows, r => r.Line?.Text is "gap-16");
        Assert.Contains(rows, r => r.Line?.Text == "gap-17");
        Assert.Contains(rows, r => r.Line?.Text == "gap-19");
    }

    [Fact]
    public void BuildCollapsibleRows_LeadingUnchangedRunHasNoChangeBefore_CollapsesAllButContextBeforeTheChange()
    {
        var leadingGap = string.Join('\n', Enumerable.Range(0, 10).Select(i => $"lead-{i}"));
        var original = $"{leadingGap}\nOLD";
        var proposed = $"{leadingGap}\nNEW";
        var diffLines = DiffBuilder.BuildLineDiff(original, proposed);

        var rows = DiffBuilder.BuildCollapsibleRows(diffLines, new HashSet<int>(), contextLines: 3);

        var placeholder = Assert.Single(rows, r => r.IsCollapsedPlaceholder);
        Assert.Equal(0, placeholder.LineIndex);
        Assert.Equal(7, placeholder.CollapsedLineCount);
        Assert.Contains(rows, r => r.Line?.Text == "lead-7");
        Assert.Contains(rows, r => r.Line?.Text == "lead-9");
    }

    [Fact]
    public void BuildCollapsibleRows_TrailingUnchangedRunHasNoChangeAfter_CollapsesAllButContextAfterTheChange()
    {
        var trailingGap = string.Join('\n', Enumerable.Range(0, 10).Select(i => $"trail-{i}"));
        var original = $"OLD\n{trailingGap}";
        var proposed = $"NEW\n{trailingGap}";
        var diffLines = DiffBuilder.BuildLineDiff(original, proposed);

        var rows = DiffBuilder.BuildCollapsibleRows(diffLines, new HashSet<int>(), contextLines: 3);

        var placeholder = Assert.Single(rows, r => r.IsCollapsedPlaceholder);
        Assert.Equal(7, placeholder.CollapsedLineCount);
        Assert.Contains(rows, r => r.Line?.Text == "trail-0");
        Assert.Contains(rows, r => r.Line?.Text == "trail-2");
        Assert.DoesNotContain(rows, r => r.Line?.Text == "trail-3");
    }

    [Fact]
    public void BuildCollapsibleRows_NoChangesAtAll_CollapsesTheWholeFile()
    {
        const string text = "a\nb\nc\nd\ne\nf\ng\nh";
        var diffLines = DiffBuilder.BuildLineDiff(text, text);

        var rows = DiffBuilder.BuildCollapsibleRows(diffLines, new HashSet<int>(), contextLines: 3);

        var placeholder = Assert.Single(rows);
        Assert.True(placeholder.IsCollapsedPlaceholder);
        Assert.Equal(diffLines.Count, placeholder.CollapsedLineCount);
    }

    [Fact]
    public void BuildCollapsibleRows_ExpandedSegment_ShowsItsRealLinesInstead()
    {
        var unchangedGap = string.Join('\n', Enumerable.Range(0, 20).Select(i => $"gap-{i}"));
        var original = $"OLD1\n{unchangedGap}\nOLD2";
        var proposed = $"NEW1\n{unchangedGap}\nNEW2";
        var diffLines = DiffBuilder.BuildLineDiff(original, proposed);
        var collapsed = DiffBuilder.BuildCollapsibleRows(diffLines, new HashSet<int>(), contextLines: 3);
        var placeholderIndex = collapsed.Single(r => r.IsCollapsedPlaceholder).LineIndex;

        var rows = DiffBuilder.BuildCollapsibleRows(diffLines, new HashSet<int> { placeholderIndex }, contextLines: 3);

        Assert.DoesNotContain(rows, r => r.IsCollapsedPlaceholder);
        Assert.Equal(diffLines.Count, rows.Count);
        Assert.Equal(diffLines, rows.Select(r => r.Line));
    }

    [Fact]
    public void BuildCollapsibleRows_ChangesCloseEnoughTogether_DoesNotCollapseTheGapBetweenThem()
    {
        // Exactly 2 * contextLines unchanged lines between two single-line changes, same as
        // GroupIntoHunks_ChangesWithinTwiceContext_MergeIntoOneHunk - close enough that the gap
        // needs to stay visible, not be collapsed.
        var unchangedGap = string.Join('\n', Enumerable.Range(0, 6).Select(i => $"gap-{i}"));
        var original = $"OLD1\n{unchangedGap}\nOLD2";
        var proposed = $"NEW1\n{unchangedGap}\nNEW2";
        var diffLines = DiffBuilder.BuildLineDiff(original, proposed);

        var rows = DiffBuilder.BuildCollapsibleRows(diffLines, new HashSet<int>(), contextLines: 3);

        Assert.DoesNotContain(rows, r => r.IsCollapsedPlaceholder);
        Assert.Equal(diffLines.Count, rows.Count);
    }

    [Fact]
    public void BuildCollapsibleRows_Empty_ReturnsNoRows()
    {
        var rows = DiffBuilder.BuildCollapsibleRows(Array.Empty<DiffLine>(), new HashSet<int>());

        Assert.Empty(rows);
    }

    [Fact]
    public void BuildCollapsibleRows_ScrollTargetsFromGroupIntoHunks_AlwaysLandOnAVisibleRow()
    {
        // Pins the invariant DiffsDetailControl.ApplyPendingScrollTarget relies on: every DiffHunk's
        // SourceRangeStart, using the same default contextLines as here, is always a real row - never
        // folded into a placeholder.
        var unchangedGap = string.Join('\n', Enumerable.Range(0, 20).Select(i => $"gap-{i}"));
        var original = $"OLD1\n{unchangedGap}\nOLD2";
        var proposed = $"NEW1\n{unchangedGap}\nNEW2";
        var diffLines = DiffBuilder.BuildLineDiff(original, proposed);
        var hunks = DiffBuilder.GroupIntoHunks(diffLines);

        var rows = DiffBuilder.BuildCollapsibleRows(diffLines, new HashSet<int>());

        foreach (var hunk in hunks)
        {
            Assert.Contains(rows, r => r.LineIndex == hunk.SourceRangeStart && !r.IsCollapsedPlaceholder);
        }
    }

    [Fact]
    public void BuildCollapsibleSideBySideRows_LongUnchangedRunBetweenChanges_CollapsesTheMiddle()
    {
        var unchangedGap = string.Join('\n', Enumerable.Range(0, 20).Select(i => $"gap-{i}"));
        var original = $"OLD1\n{unchangedGap}\nOLD2";
        var proposed = $"NEW1\n{unchangedGap}\nNEW2";
        var diffLines = DiffBuilder.BuildLineDiff(original, proposed);

        var rows = DiffBuilder.BuildCollapsibleSideBySideRows(diffLines, new HashSet<int>(), contextLines: 3);

        var placeholder = Assert.Single(rows, r => r.IsCollapsedPlaceholder);
        Assert.Equal(20 - 3 - 3, placeholder.CollapsedLineCount);
        Assert.Contains(rows, r => r.Row?.OriginalText == "OLD1" && r.Row.ProposedText == "NEW1");
        Assert.Contains(rows, r => r.Row?.OriginalText == "OLD2" && r.Row.ProposedText == "NEW2");
    }

    [Fact]
    public void BuildCollapsibleSideBySideRows_ExpandedSegment_PairsItsRealLinesInstead()
    {
        var unchangedGap = string.Join('\n', Enumerable.Range(0, 20).Select(i => $"gap-{i}"));
        var original = $"OLD1\n{unchangedGap}\nOLD2";
        var proposed = $"NEW1\n{unchangedGap}\nNEW2";
        var diffLines = DiffBuilder.BuildLineDiff(original, proposed);
        var collapsed = DiffBuilder.BuildCollapsibleSideBySideRows(diffLines, new HashSet<int>(), contextLines: 3);
        var placeholderIndex = collapsed.Single(r => r.IsCollapsedPlaceholder).LineIndex;

        var rows = DiffBuilder.BuildCollapsibleSideBySideRows(diffLines, new HashSet<int> { placeholderIndex }, contextLines: 3);

        Assert.DoesNotContain(rows, r => r.IsCollapsedPlaceholder);
        Assert.Contains(rows, r => r.Row?.OriginalText == "gap-6" && r.Row.ProposedText == "gap-6");
    }

    [Fact]
    public void BuildCollapsibleSideBySideRows_Empty_ReturnsNoRows()
    {
        var rows = DiffBuilder.BuildCollapsibleSideBySideRows(Array.Empty<DiffLine>(), new HashSet<int>());

        Assert.Empty(rows);
    }

    [Fact]
    public void BuildWordDiff_IdenticalLines_ReturnsAllUnchanged()
    {
        var tokens = DiffBuilder.BuildWordDiff("the quick fox", "the quick fox");

        Assert.All(tokens, t => Assert.Equal(DiffLineKind.Unchanged, t.Kind));
        Assert.Equal("the quick fox", string.Concat(tokens.Select(t => t.Text)));
    }

    [Fact]
    public void BuildWordDiff_OneWordChanged_MarksOnlyThatWord()
    {
        var tokens = DiffBuilder.BuildWordDiff("the quick fox", "the slow fox");

        Assert.Equal(
            [
                ("the", DiffLineKind.Unchanged),
                (" ", DiffLineKind.Unchanged),
                ("quick", DiffLineKind.Removed),
                ("slow", DiffLineKind.Added),
                (" ", DiffLineKind.Unchanged),
                ("fox", DiffLineKind.Unchanged),
            ],
            tokens.Select(t => (t.Text, t.Kind)));
    }

    [Fact]
    public void BuildWordDiff_BothTokenListsReconstructTheirOwnLineExactly()
    {
        // Lossless tokenizing (word runs, non-word runs, and whitespace all captured) means every
        // token concatenated back together must reproduce the original line - true of the whole
        // diff's tokens regardless of which side they came from.
        var tokens = DiffBuilder.BuildWordDiff("foo-bar(baz)", "foo-qux(baz)");

        var original = string.Concat(tokens.Where(t => t.Kind != DiffLineKind.Added).Select(t => t.Text));
        var proposed = string.Concat(tokens.Where(t => t.Kind != DiffLineKind.Removed).Select(t => t.Text));

        Assert.Equal("foo-bar(baz)", original);
        Assert.Equal("foo-qux(baz)", proposed);
    }

    [Fact]
    public void BuildWordDiff_CompletelyDifferentLines_MarksEverythingChanged()
    {
        var tokens = DiffBuilder.BuildWordDiff("abc", "xyz");

        Assert.Equal(
            [("abc", DiffLineKind.Removed), ("xyz", DiffLineKind.Added)],
            tokens.Select(t => (t.Text, t.Kind)));
    }

    [Fact]
    public void BuildSideBySideRows_PairedChangedLine_PopulatesWordSpansOnBothSides()
    {
        var diffLines = DiffBuilder.BuildLineDiff("the quick fox", "the slow fox");

        var rows = DiffBuilder.BuildSideBySideRows(diffLines);

        var row = Assert.Single(rows);
        Assert.NotNull(row.OriginalWordSpans);
        Assert.NotNull(row.ProposedWordSpans);
        Assert.Equal("the quick fox", string.Concat(row.OriginalWordSpans!.Select(s => s.Text)));
        Assert.Equal("the slow fox", string.Concat(row.ProposedWordSpans!.Select(s => s.Text)));
        Assert.Contains(row.OriginalWordSpans!, s => s is { Text: "quick", Kind: DiffLineKind.Removed });
        Assert.Contains(row.ProposedWordSpans!, s => s is { Text: "slow", Kind: DiffLineKind.Added });
    }

    [Fact]
    public void BuildSideBySideRows_UnchangedLine_HasNoWordSpans()
    {
        var diffLines = DiffBuilder.BuildLineDiff("line1", "line1");

        var rows = DiffBuilder.BuildSideBySideRows(diffLines);

        var row = Assert.Single(rows);
        Assert.Null(row.OriginalWordSpans);
        Assert.Null(row.ProposedWordSpans);
    }

    [Fact]
    public void BuildSideBySideRows_PaddedUnpairedRow_HasNoWordSpans()
    {
        var diffLines = DiffBuilder.BuildLineDiff("a", "x\ny");

        var rows = DiffBuilder.BuildSideBySideRows(diffLines);

        // rows[1] is "y" padded against a blank original cell - nothing to diff it against.
        Assert.Null(rows[1].OriginalWordSpans);
        Assert.Null(rows[1].ProposedWordSpans);
    }

    [Fact]
    public void BuildCollapsibleRows_PairedChangedLine_PopulatesWordSpansOnBothLines()
    {
        var diffLines = DiffBuilder.BuildLineDiff("the quick fox", "the slow fox");

        var rows = DiffBuilder.BuildCollapsibleRows(diffLines, new HashSet<int>());

        var removedRow = rows.Single(r => r.Line?.Kind == DiffLineKind.Removed);
        var addedRow = rows.Single(r => r.Line?.Kind == DiffLineKind.Added);
        Assert.NotNull(removedRow.WordSpans);
        Assert.NotNull(addedRow.WordSpans);
        Assert.Contains(removedRow.WordSpans!, s => s is { Text: "quick", Kind: DiffLineKind.Removed });
        Assert.Contains(addedRow.WordSpans!, s => s is { Text: "slow", Kind: DiffLineKind.Added });
    }

    [Fact]
    public void BuildCollapsibleRows_UnchangedLine_HasNoWordSpans()
    {
        var diffLines = DiffBuilder.BuildLineDiff("line1", "line1");

        var rows = DiffBuilder.BuildCollapsibleRows(diffLines, new HashSet<int>());

        Assert.Null(Assert.Single(rows).WordSpans);
    }

    [Fact]
    public void BuildCollapsibleRows_UnpairedChange_HasNoWordSpans()
    {
        var diffLines = DiffBuilder.BuildLineDiff("a", "x\ny");

        var rows = DiffBuilder.BuildCollapsibleRows(diffLines, new HashSet<int>());

        // "a" pairs against "x", leaving "y" with no partner to diff against.
        var unpairedAdded = rows.Single(r => r.Line is { Text: "y" });
        Assert.Null(unpairedAdded.WordSpans);
    }

    [Fact]
    public void BuildCollapsibleSideBySideRows_PairedChangedLine_PopulatesWordSpansViaRow()
    {
        var diffLines = DiffBuilder.BuildLineDiff("the quick fox", "the slow fox");

        var rows = DiffBuilder.BuildCollapsibleSideBySideRows(diffLines, new HashSet<int>());

        var row = Assert.Single(rows).Row;
        Assert.NotNull(row!.OriginalWordSpans);
        Assert.NotNull(row.ProposedWordSpans);
    }
}
