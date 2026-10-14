using CodeGenToTutorial.Core.Models;

namespace CodeGenToTutorial.Core.Contracts.Services;

// Session-scoped, append-only log of apply/undo/revert actions (T4.2.3), for display on the
// Tutorial page - distinct from IApplyHistoryStore's undo stack, which holds full file content and
// shrinks as it's undone, and from IApplyBackupStore, which has no concept of individual actions
// or timestamps at all. Registered as a singleton so it survives page navigation for the lifetime
// of the running app, like the other apply-related stores.
public interface IApplyHistoryLog
{
    // No-ops for an empty FilePaths list - callers record a batch/undo/revert regardless of
    // whether it actually touched any files, so there's nothing worth logging.
    void Record(ApplyHistoryEntryKind kind, IReadOnlyList<string> filePaths);

    // Oldest first (insertion order) - the caller decides how to order these for display.
    IReadOnlyList<ApplyHistoryEntry> Entries { get; }
}
