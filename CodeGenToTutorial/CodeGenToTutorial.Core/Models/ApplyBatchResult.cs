namespace CodeGenToTutorial.Core.Models;

// Result of an ApplyOrchestrationService.ApplySelected batch (T3.3.1/T3.3.4). Only files that had
// something selected to apply are represented here - a file nothing was selected on was never
// attempted, so it has no outcome to report. One failed or conflicted file never stops the rest of
// the batch (T3.3.4) - every selected file gets its own attempt and its own entry here.
public record ApplyBatchResult(IReadOnlyList<FileApplyResult> Results)
{
    public int AppliedCount => Results.Count(r => r.Outcome == FileApplyOutcome.Applied);

    public int ConflictCount => Results.Count(r => r.Outcome == FileApplyOutcome.Conflict);

    public int FailedCount => Results.Count(r => r.Outcome == FileApplyOutcome.Failed);

    public IEnumerable<FileApplyResult> ConflictedFiles => Results.Where(r => r.Outcome == FileApplyOutcome.Conflict);

    public IEnumerable<FileApplyResult> FailedFiles => Results.Where(r => r.Outcome == FileApplyOutcome.Failed);
}
