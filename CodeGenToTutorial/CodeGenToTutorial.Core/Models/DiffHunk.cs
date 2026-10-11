namespace CodeGenToTutorial.Core.Models;

// One contiguous region of a unified-diff-style grouping of DiffLines: a run of changed lines plus
// up to N lines of surrounding unchanged context on each side. Hunks whose changes are closer
// together than twice the context window are merged into a single hunk rather than left to overlap.
// Line numbers are 1-based, matching the unified diff convention; a zero count means the hunk is a
// pure insertion/deletion at that side, in which case the start line is the 0-based insertion point.
// SourceRangeStart/SourceRangeEnd are the 0-based [start, end) index range this hunk occupies within
// the flat DiffLines list it was grouped from (DiffBuilder.GroupIntoHunks) - needed by
// DiffBuilder.ComposeContent to know which lines belong to which hunk when recomposing a file from a
// subset of selected hunks.
public record DiffHunk(
    int OriginalStartLine,
    int OriginalLineCount,
    int ProposedStartLine,
    int ProposedLineCount,
    IReadOnlyList<DiffLine> Lines,
    int SourceRangeStart,
    int SourceRangeEnd)
{
    // Standard unified-diff hunk header, e.g. "@@ -12,5 +12,7 @@".
    public string Header => $"@@ -{OriginalStartLine},{OriginalLineCount} +{ProposedStartLine},{ProposedLineCount} @@";
}
