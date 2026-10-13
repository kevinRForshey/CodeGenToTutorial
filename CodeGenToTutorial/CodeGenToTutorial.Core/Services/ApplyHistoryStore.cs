using CodeGenToTutorial.Core.Contracts.Services;
using CodeGenToTutorial.Core.Models;

namespace CodeGenToTutorial.Core.Services;

// In-memory LIFO stack of applied batches. Not thread-safe: like ApplyBackupStore, every caller
// today is the UI thread driving ApplyOrchestrationService from a RelayCommand.
public class ApplyHistoryStore : IApplyHistoryStore
{
    private readonly Stack<IReadOnlyList<AppliedFileChange>> _batches = new();

    public bool HasHistory => _batches.Count > 0;

    public void RecordBatch(IReadOnlyList<AppliedFileChange> changes)
    {
        if (changes.Count == 0)
        {
            return;
        }

        _batches.Push(changes);
    }

    public bool TryPopLastBatch(out IReadOnlyList<AppliedFileChange> changes)
    {
        if (_batches.Count == 0)
        {
            changes = Array.Empty<AppliedFileChange>();
            return false;
        }

        changes = _batches.Pop();
        return true;
    }

    public void Clear() => _batches.Clear();
}
