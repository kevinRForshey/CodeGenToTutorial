using Paarde.Core.Models;

namespace Paarde.Core.Contracts.Services;

// Persists a durable record of each prompt run to disk (T7.1.1). Registered as a singleton (like
// IPromptResultStore/IApplyHistoryLog) so it survives page navigation and can observe both stores
// directly rather than having their state threaded through every call.
public interface IRunSessionStore
{
    // Starts a new session: caches prompt/rawOutput, then writes a snapshot of the current
    // IPromptResultStore (Tutorial/Files/WorkingDirectoryPath) and IApplyHistoryLog to a new file.
    // Called once per completed run (success or failure) - never for a run that was cancelled or
    // threw before producing a CLI result, since there's nothing meaningful to persist for those.
    void RecordRun(string prompt, string rawOutput);

    // Re-reads the current IPromptResultStore (picking up any selection changes) and
    // IApplyHistoryLog (picking up newly recorded apply/undo/revert entries) and rewrites the most
    // recent session's file with them, keeping its cached prompt/rawOutput unchanged. A no-op if
    // RecordRun hasn't been called yet.
    void Resave();

    // Loads the most recently recorded session (by its sortable filename, newest last) and pushes
    // it into IPromptResultStore and IApplyHistoryLog, so the app comes back up showing the same
    // tutorial/diffs and apply history it had when it last closed (T7.1.2). Also adopts the loaded
    // session as the active one, so a later Resave() keeps updating that same file instead of
    // leaving it behind. Returns null (and touches neither store) if no session has ever been
    // recorded.
    RunSession? RestoreLastSession();
}
