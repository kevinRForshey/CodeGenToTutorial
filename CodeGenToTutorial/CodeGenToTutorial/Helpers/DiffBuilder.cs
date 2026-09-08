using CodeGenToTutorial.Models;

namespace CodeGenToTutorial.Helpers;

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
