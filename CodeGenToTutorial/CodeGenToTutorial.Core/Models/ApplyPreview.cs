namespace CodeGenToTutorial.Core.Models;

// Summary of what an ApplyOrchestrationService.ApplySelected call would do, shown in a confirmation
// prompt before actually touching disk (T3.3.1). ChangeCount counts each selected step/hunk
// individually, and a whole-file fallback (a file with no resolved hunks, applied via
// ProposedFileChangeViewModel.IsSelected directly) counts as a single change.
//
// ProtectedPathCount/ProtectedPathFiles (T4.1.2) are additional properties rather than primary-
// constructor parameters so every existing `new ApplyPreview(fileCount, changeCount)` call site -
// tests included - keeps compiling and keeps meaning "no protected files" (the default for both),
// without needing to be touched.
public record ApplyPreview(int FileCount, int ChangeCount)
{
    public int ProtectedPathCount { get; init; }

    public IReadOnlyList<string> ProtectedPathFiles { get; init; } = Array.Empty<string>();
}
