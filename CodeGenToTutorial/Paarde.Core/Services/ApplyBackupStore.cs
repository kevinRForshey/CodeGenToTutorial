using Paarde.Core.Contracts.Services;

namespace Paarde.Core.Services;

// In-memory, keyed by the proposal's relative FilePath - matches how FileApplyResult/
// ProposedFileChangeViewModel already identify a file elsewhere in the apply flow. Not
// thread-safe: like IPromptResultStore, every caller today is the UI thread driving
// ApplyOrchestrationService from a RelayCommand.
public class ApplyBackupStore : IApplyBackupStore
{
    private readonly Dictionary<string, string> _snapshots = new();

    public void SnapshotIfAbsent(string filePath, string content)
    {
        if (!_snapshots.ContainsKey(filePath))
        {
            _snapshots[filePath] = content;
        }
    }

    public bool TryGetSnapshot(string filePath, out string content) =>
        _snapshots.TryGetValue(filePath, out content!);

    public IReadOnlyDictionary<string, string> AllSnapshots => _snapshots;
}
