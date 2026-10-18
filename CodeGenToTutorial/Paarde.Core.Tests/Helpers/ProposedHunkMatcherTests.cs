using Paarde.Core.Helpers;
using Paarde.Core.Models;

namespace Paarde.Core.Tests.Helpers;

public class ProposedHunkMatcherTests
{
    [Fact]
    public void FindMatches_NullLocation_ReturnsEmpty()
    {
        var diffHunks = DiffBuilder.GroupIntoHunks(DiffBuilder.BuildLineDiff("a", "b"));

        var matches = ProposedHunkMatcher.FindMatches(diffHunks, null);

        Assert.Empty(matches);
    }

    [Fact]
    public void FindMatches_LocationWithinSingleHunk_ReturnsThatHunk()
    {
        var original = "a\nb\nc\nOLD\nd\ne\nf";
        var proposed = "a\nb\nc\nNEW\nd\ne\nf";
        var diffLines = DiffBuilder.BuildLineDiff(original, proposed);
        var diffHunks = DiffBuilder.GroupIntoHunks(diffLines);

        // "NEW" is proposed-content line index 3 (0-based).
        var matches = ProposedHunkMatcher.FindMatches(diffHunks, new HunkLocation(3, 3));

        Assert.Equal(diffHunks, matches);
    }

    [Fact]
    public void FindMatches_LocationOutsideAnyHunk_ReturnsEmpty()
    {
        var unchangedGap = string.Join('\n', Enumerable.Range(0, 20).Select(i => $"gap-{i}"));
        var original = $"OLD1\n{unchangedGap}\nOLD2";
        var proposed = $"NEW1\n{unchangedGap}\nNEW2";
        var diffLines = DiffBuilder.BuildLineDiff(original, proposed);
        var diffHunks = DiffBuilder.GroupIntoHunks(diffLines, contextLines: 3);
        Assert.Equal(2, diffHunks.Count);

        // A location in the unchanged gap, far from either hunk's context window.
        var matches = ProposedHunkMatcher.FindMatches(diffHunks, new HunkLocation(10, 10));

        Assert.Empty(matches);
    }

    [Fact]
    public void FindMatches_LocationSpanningTwoHunks_ReturnsBoth()
    {
        var unchangedGap = string.Join('\n', Enumerable.Range(0, 20).Select(i => $"gap-{i}"));
        var original = $"OLD1\n{unchangedGap}\nOLD2";
        var proposed = $"NEW1\n{unchangedGap}\nNEW2";
        var diffLines = DiffBuilder.BuildLineDiff(original, proposed);
        var diffHunks = DiffBuilder.GroupIntoHunks(diffLines, contextLines: 3);
        Assert.Equal(2, diffHunks.Count);

        var lastLine = proposed.Split('\n').Length - 1;
        var matches = ProposedHunkMatcher.FindMatches(diffHunks, new HunkLocation(0, lastLine));

        Assert.Equal(2, matches.Count);
    }

    [Fact]
    public void FindMatches_PureDeletionHunk_NeverMatches()
    {
        // Deleting a file's entire content produces a hunk with ProposedLineCount == 0 - there's no
        // proposed-side range for a step's snippet to land in.
        var diffLines = DiffBuilder.BuildLineDiff("OLD", string.Empty);
        var diffHunks = DiffBuilder.GroupIntoHunks(diffLines);
        var hunk = Assert.Single(diffHunks);
        Assert.Equal(0, hunk.ProposedLineCount);

        var matches = ProposedHunkMatcher.FindMatches(diffHunks, new HunkLocation(0, 0));

        Assert.Empty(matches);
    }
}
