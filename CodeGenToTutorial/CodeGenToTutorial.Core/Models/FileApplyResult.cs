namespace CodeGenToTutorial.Core.Models;

public enum FileApplyOutcome
{
    // The file's selected changes were written to disk.
    Applied,

    // The file changed on disk since this proposal's OriginalContent baseline was captured; the
    // write was skipped rather than silently overwriting whatever the user (or another process) did
    // to the file in the meantime. The caller can offer ProposedFileChangeViewModel.RefreshOriginalContent
    // to re-diff against the current disk content and try again.
    Conflict,

    // The write was attempted and threw (I/O error, permissions, or the path-containment check).
    Failed,
}

// The outcome of attempting to apply one file's selected changes, as part of an
// ApplyOrchestrationService.ApplySelected batch. ErrorMessage is only ever set for Failed.
public record FileApplyResult(string FilePath, FileApplyOutcome Outcome, string? ErrorMessage = null);
