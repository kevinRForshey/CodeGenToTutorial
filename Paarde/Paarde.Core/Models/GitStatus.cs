namespace Paarde.Core.Models;

// Result of asking IGitStatusService whether a working directory sits inside a git repository and,
// if so, which files are currently dirty there (T4.3.1). DirtyFiles are relative paths in the same
// forward-slash convention as ProposedFileChangeViewModel.FilePath, so callers can intersect the
// two directly without extra normalization.
public record GitStatus(bool IsRepository, IReadOnlyList<string> DirtyFiles)
{
    public static readonly GitStatus NotARepository = new(IsRepository: false, DirtyFiles: Array.Empty<string>());

    public bool IsDirty => DirtyFiles.Count > 0;
}
