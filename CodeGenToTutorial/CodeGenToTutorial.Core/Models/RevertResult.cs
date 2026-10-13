namespace CodeGenToTutorial.Core.Models;

// One file's outcome from writing a prior content snapshot back to disk, as part of a
// TryUndoLastApply or RevertAllApplied call (T4.2.2). ErrorMessage is only ever set when
// Succeeded is false.
public record RevertFileResult(string FilePath, bool Succeeded, string? ErrorMessage = null);

// Result of an ApplyOrchestrationService.TryUndoLastApply or RevertAllApplied call (T4.2.2).
// Mirrors ApplyBatchResult's shape: one failed file never stops the rest from being restored.
public record RevertResult(IReadOnlyList<RevertFileResult> Results)
{
    public int SucceededCount => Results.Count(r => r.Succeeded);

    public int FailedCount => Results.Count(r => !r.Succeeded);

    public IEnumerable<RevertFileResult> FailedFiles => Results.Where(r => !r.Succeeded);
}
