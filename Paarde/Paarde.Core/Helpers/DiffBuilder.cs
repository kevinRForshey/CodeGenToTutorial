using Paarde.Core.Models;

namespace Paarde.Core.Helpers;

// Builds a line-level diff (which lines were added/removed/kept) between an original and proposed
// file's contents, using a standard LCS dynamic-programming table.
public static class DiffBuilder
{
    // Above this many original*proposed line pairs the O(n*m) LCS table gets too large; fall back to
    // treating the whole file as replaced rather than risk excessive memory use on huge files.
    private const int MaxLcsCells = 4_000_000;

    public static IReadOnlyList<DiffLine> BuildLineDiff(string originalText, string proposedText)
    {
        var originalLines = SplitLines(originalText);
        var proposedLines = SplitLines(proposedText);

        if ((long)originalLines.Length * proposedLines.Length > MaxLcsCells)
        {
            return originalLines.Select(line => new DiffLine(DiffLineKind.Removed, line))
                .Concat(proposedLines.Select(line => new DiffLine(DiffLineKind.Added, line)))
                .ToList();
        }

        var lcsLengths = ComputeLcsLengths(originalLines, proposedLines);

        var result = new List<DiffLine>();
        Backtrack(lcsLengths, originalLines, proposedLines, result);
        return result;
    }

    // T8.3.1: pairs up a flat line-level diff into side-by-side rows for the diff viewer's
    // side-by-side mode. Each maximal run of consecutive Removed/Added lines is split back into
    // its removed-only and added-only sub-sequences (preserving each's own relative order) and
    // paired off index-by-index, so a changed line's old and new text land on the same row -
    // padding the shorter side with a blank cell (null text/kind) when the run has more lines on
    // one side than the other. An Unchanged line always gets its own row with identical text on
    // both sides.
    public static IReadOnlyList<SideBySideDiffRow> BuildSideBySideRows(IReadOnlyList<DiffLine> diffLines)
    {
        var rows = new List<SideBySideDiffRow>();
        var removedRun = new List<DiffLine>();
        var addedRun = new List<DiffLine>();

        void FlushRun()
        {
            var pairCount = Math.Max(removedRun.Count, addedRun.Count);

            for (var i = 0; i < pairCount; i++)
            {
                var left = i < removedRun.Count ? removedRun[i] : null;
                var right = i < addedRun.Count ? addedRun[i] : null;
                rows.Add(new SideBySideDiffRow(left?.Text, left?.Kind, right?.Text, right?.Kind));
            }

            removedRun.Clear();
            addedRun.Clear();
        }

        foreach (var line in diffLines)
        {
            switch (line.Kind)
            {
                case DiffLineKind.Removed:
                    removedRun.Add(line);
                    break;
                case DiffLineKind.Added:
                    addedRun.Add(line);
                    break;
                default:
                    FlushRun();
                    rows.Add(new SideBySideDiffRow(line.Text, line.Kind, line.Text, line.Kind));
                    break;
            }
        }

        FlushRun();

        return rows;
    }

    // Groups a flat line-level diff into unified-diff-style hunks: each changed region plus up to
    // contextLines of surrounding unchanged lines. Changed regions separated by no more than
    // 2 * contextLines of unchanged lines are merged into one hunk (their shared context would
    // otherwise overlap); regions farther apart than that become separate hunks, and runs of
    // unchanged lines outside any hunk's context are dropped rather than carried through.
    public static IReadOnlyList<DiffHunk> GroupIntoHunks(IReadOnlyList<DiffLine> diffLines, int contextLines = 3)
    {
        var changeRanges = FindChangeRanges(diffLines);

        if (changeRanges.Count == 0)
        {
            return Array.Empty<DiffHunk>();
        }

        var mergedRanges = MergeNearbyRanges(changeRanges, contextLines);
        var originalPrefixCounts = ComputePrefixCounts(diffLines, excludedKind: DiffLineKind.Added);
        var proposedPrefixCounts = ComputePrefixCounts(diffLines, excludedKind: DiffLineKind.Removed);

        var hunks = new List<DiffHunk>(mergedRanges.Count);

        foreach (var (changeStart, changeEnd) in mergedRanges)
        {
            var start = Math.Max(0, changeStart - contextLines);
            var end = Math.Min(diffLines.Count, changeEnd + contextLines);
            hunks.Add(BuildHunk(diffLines, start, end, originalPrefixCounts, proposedPrefixCounts));
        }

        return hunks;
    }

    // Finds the maximal runs of consecutive non-Unchanged lines, as [start, end) index pairs.
    private static List<(int Start, int End)> FindChangeRanges(IReadOnlyList<DiffLine> diffLines)
    {
        var ranges = new List<(int Start, int End)>();
        var i = 0;

        while (i < diffLines.Count)
        {
            if (diffLines[i].Kind == DiffLineKind.Unchanged)
            {
                i++;
                continue;
            }

            var start = i;

            while (i < diffLines.Count && diffLines[i].Kind != DiffLineKind.Unchanged)
            {
                i++;
            }

            ranges.Add((start, i));
        }

        return ranges;
    }

    // Merges adjacent change ranges whose unchanged gap is small enough that their context windows
    // would overlap.
    private static List<(int Start, int End)> MergeNearbyRanges(List<(int Start, int End)> ranges, int contextLines)
    {
        var merged = new List<(int Start, int End)> { ranges[0] };

        for (var i = 1; i < ranges.Count; i++)
        {
            var last = merged[^1];
            var gap = ranges[i].Start - last.End;

            if (gap <= contextLines * 2)
            {
                merged[^1] = (last.Start, ranges[i].End);
            }
            else
            {
                merged.Add(ranges[i]);
            }
        }

        return merged;
    }

    // counts[i] = number of lines among diffLines[0..i) that consume a line of the side being
    // counted (i.e. everything except the kind that side doesn't contribute to).
    private static int[] ComputePrefixCounts(IReadOnlyList<DiffLine> diffLines, DiffLineKind excludedKind)
    {
        var counts = new int[diffLines.Count + 1];

        for (var i = 0; i < diffLines.Count; i++)
        {
            counts[i + 1] = counts[i] + (diffLines[i].Kind != excludedKind ? 1 : 0);
        }

        return counts;
    }

    private static DiffHunk BuildHunk(
        IReadOnlyList<DiffLine> diffLines,
        int start,
        int end,
        int[] originalPrefixCounts,
        int[] proposedPrefixCounts)
    {
        var lines = diffLines.Skip(start).Take(end - start).ToList();
        var originalLineCount = originalPrefixCounts[end] - originalPrefixCounts[start];
        var proposedLineCount = proposedPrefixCounts[end] - proposedPrefixCounts[start];

        // A zero count means this side contributes nothing (a pure insertion or pure deletion); the
        // unified diff convention is then to report the insertion point rather than a 1-based line.
        var originalStartLine = originalLineCount > 0 ? originalPrefixCounts[start] + 1 : originalPrefixCounts[start];
        var proposedStartLine = proposedLineCount > 0 ? proposedPrefixCounts[start] + 1 : proposedPrefixCounts[start];

        return new DiffHunk(originalStartLine, originalLineCount, proposedStartLine, proposedLineCount, lines, start, end);
    }

    // Recomposes file content from diffLines, taking each hunk's proposed (Added) lines where
    // isSelected approves it and its original (Removed) lines otherwise, so an unselected hunk's
    // change is left out entirely rather than partially applied. Lines outside every hunk's
    // SourceRangeStart/End are always Unchanged (hunks are built to cover every changed region) and
    // pass through regardless of selection.
    public static string ComposeContent(IReadOnlyList<DiffLine> diffLines, IReadOnlyList<DiffHunk> hunks, Func<DiffHunk, bool> isSelected)
    {
        var result = new List<string>(diffLines.Count);
        var hunkIndex = 0;

        for (var i = 0; i < diffLines.Count; i++)
        {
            while (hunkIndex < hunks.Count && i >= hunks[hunkIndex].SourceRangeEnd)
            {
                hunkIndex++;
            }

            var line = diffLines[i];
            var inCurrentHunk = hunkIndex < hunks.Count && i >= hunks[hunkIndex].SourceRangeStart;

            if (!inCurrentHunk)
            {
                // Outside any hunk's range - guaranteed Unchanged, since GroupIntoHunks' change
                // ranges (plus context) are exactly what became hunks.
                result.Add(line.Text);
                continue;
            }

            var keepAdded = isSelected(hunks[hunkIndex]);

            if (line.Kind == DiffLineKind.Unchanged || (line.Kind == DiffLineKind.Added && keepAdded) || (line.Kind == DiffLineKind.Removed && !keepAdded))
            {
                result.Add(line.Text);
            }
        }

        return string.Join('\n', result);
    }

    private static string[] SplitLines(string text) =>
        string.IsNullOrEmpty(text) ? Array.Empty<string>() : text.Replace("\r\n", "\n").Split('\n');

    private static int[,] ComputeLcsLengths(string[] original, string[] proposed)
    {
        var lengths = new int[original.Length + 1, proposed.Length + 1];

        for (var i = original.Length - 1; i >= 0; i--)
        {
            for (var j = proposed.Length - 1; j >= 0; j--)
            {
                lengths[i, j] = original[i] == proposed[j]
                    ? lengths[i + 1, j + 1] + 1
                    : Math.Max(lengths[i + 1, j], lengths[i, j + 1]);
            }
        }

        return lengths;
    }

    private static void Backtrack(int[,] lengths, string[] original, string[] proposed, List<DiffLine> result)
    {
        var i = 0;
        var j = 0;

        while (i < original.Length && j < proposed.Length)
        {
            if (original[i] == proposed[j])
            {
                result.Add(new DiffLine(DiffLineKind.Unchanged, original[i]));
                i++;
                j++;
            }
            else if (lengths[i + 1, j] >= lengths[i, j + 1])
            {
                result.Add(new DiffLine(DiffLineKind.Removed, original[i]));
                i++;
            }
            else
            {
                result.Add(new DiffLine(DiffLineKind.Added, proposed[j]));
                j++;
            }
        }

        while (i < original.Length)
        {
            result.Add(new DiffLine(DiffLineKind.Removed, original[i]));
            i++;
        }

        while (j < proposed.Length)
        {
            result.Add(new DiffLine(DiffLineKind.Added, proposed[j]));
            j++;
        }
    }
}
