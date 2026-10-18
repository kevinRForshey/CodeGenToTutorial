namespace CodeGenToTutorial.Core.Models;

// One step's selection state within a RunSessionFile (T7.1.1) - the persisted counterpart of
// ProposedHunk. Only what's needed to restore IsSelected is kept; DiffHunks/MatchedDiffHunks are
// derived state that ProposedFileChangeViewModel recomputes from OriginalContent/ProposedContent/
// EditedContent/Hunks on construction, so persisting them would be redundant - and IsApplied
// always resets to false on load anyway (OnOriginalContentChanged), so persisting it would be
// meaningless.
public record RunSessionHunk(string Title, string Explanation, string Snippet, HunkLocation? Location, bool IsSelected);

// One proposed file within a RunSession - the persisted counterpart of ProposedFileChangeViewModel.
// WholeFileSelected is only meaningful when Hunks is empty (the fallback whole-file selection,
// ProposedFileChangeViewModel.IsSelected when HasSelectableHunks is false); it's ignored otherwise.
public record RunSessionFile(
    string FilePath,
    string OriginalContent,
    string ProposedContent,
    string EditedContent,
    bool WholeFileSelected,
    IReadOnlyList<RunSessionHunk> Hunks);

// A durable, on-disk snapshot of one prompt run (T7.1.1): the exact prompt sent, the CLI's raw
// output, the parsed tutorial/files (with their current selections), and the apply-history entries
// recorded against this run so far. Written by IRunSessionStore.RecordRun right after a run
// completes and kept current by Resave as the user applies/undoes/reverts changes - see
// IRunSessionStore for exactly when each happens. Restoring one of these into the running app is
// T7.1.2, not yet implemented.
public record RunSession(
    string Id,
    DateTimeOffset StartedAt,
    string WorkingDirectoryPath,
    string Prompt,
    string RawOutput,
    string Tutorial,
    IReadOnlyList<RunSessionFile> Files,
    IReadOnlyList<ApplyHistoryEntry> ApplyLog);
