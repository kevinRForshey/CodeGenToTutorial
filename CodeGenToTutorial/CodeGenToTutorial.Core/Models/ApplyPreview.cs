namespace CodeGenToTutorial.Core.Models;

// Cheap, I/O-free summary of what an ApplyOrchestrationService.ApplySelected call would do, shown in
// a confirmation prompt before actually touching disk (T3.3.1). ChangeCount counts each selected
// step/hunk individually, and a whole-file fallback (a file with no resolved hunks, applied via
// ProposedFileChangeViewModel.IsSelected directly) counts as a single change.
public record ApplyPreview(int FileCount, int ChangeCount);
