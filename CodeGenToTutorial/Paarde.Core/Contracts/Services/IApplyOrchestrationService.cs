using Paarde.Core.Models;
using Paarde.Core.ViewModels;

namespace Paarde.Core.Contracts.Services;

public interface IApplyOrchestrationService
{
    // Summary for a confirmation prompt before ApplySelected actually touches disk. Reads
    // .gitignore under workingDirectoryPath (T4.1.2) to report ProtectedPathCount/ProtectedPathFiles,
    // and shells out to git (T4.3.1) to report IsGitRepository/GitDirtyFileCount/GitDirtyFiles for
    // the files about to be applied, so no longer strictly I/O-free, but still cheap (one file
    // read, one git invocation, no writes).
    ApplyPreview Preview(IReadOnlyList<ProposedFileChangeViewModel> files, string workingDirectoryPath);

    // Applies every file that has something selected, independently of the others - one file's
    // conflict or failure never stops the rest of the batch (T3.3.4). A file resolving to a
    // protected path (.git/bin/obj or .gitignore) is skipped as ProtectedPathBlocked unless
    // allowProtectedPaths is true, meaning the caller already confirmed that with the user (T4.1.2).
    ApplyBatchResult ApplySelected(IReadOnlyList<ProposedFileChangeViewModel> files, string workingDirectoryPath, bool allowProtectedPaths = false);

    // Undoes the most recently applied ApplySelected batch (T4.2.2), restoring each of its files'
    // pre-apply content. False (with an empty result) if there's no recorded batch left to undo.
    bool TryUndoLastApply(string workingDirectoryPath, out RevertResult result);

    // Restores every file this session has applied a change to back to its content from before
    // this session touched it at all (T4.2.2), not just the most recent batch.
    RevertResult RevertAllApplied(string workingDirectoryPath);
}
