using Paarde.Core.Contracts.Services;
using Paarde.Core.Models;

namespace Paarde.Core.Services;

// In-memory, insertion-ordered list. Not thread-safe: like the other apply-related stores, every
// caller today is the UI thread driving ApplyOrchestrationService from a RelayCommand.
public class ApplyHistoryLog : IApplyHistoryLog
{
    private readonly List<ApplyHistoryEntry> _entries = new();

    public IReadOnlyList<ApplyHistoryEntry> Entries => _entries;

    public void Record(ApplyHistoryEntryKind kind, IReadOnlyList<string> filePaths)
    {
        if (filePaths.Count == 0)
        {
            return;
        }

        _entries.Add(new ApplyHistoryEntry(DateTimeOffset.UtcNow, kind, filePaths));
    }

    public void Restore(IReadOnlyList<ApplyHistoryEntry> entries)
    {
        _entries.Clear();
        _entries.AddRange(entries);
    }
}
