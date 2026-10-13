using CodeGenToTutorial.Core.Models;
using CodeGenToTutorial.Core.ViewModels;

namespace CodeGenToTutorial.Core.Contracts.Services;

public interface IApplyOrchestrationService
{
    // Summary for a confirmation prompt before ApplySelected actually touches disk. Reads
    // .gitignore under workingDirectoryPath (T4.1.2) to report ProtectedPathCount/ProtectedPathFiles,
    // so no longer strictly I/O-free, but still cheap (one file read, no writes).
    ApplyPreview Preview(IReadOnlyList<ProposedFileChangeViewModel> files, string workingDirectoryPath);

    // Applies every file that has something selected, independently of the others - one file's
    // conflict or failure never stops the rest of the batch (T3.3.4). A file resolving to a
    // protected path (.git/bin/obj or .gitignore) is skipped as ProtectedPathBlocked unless
    // allowProtectedPaths is true, meaning the caller already confirmed that with the user (T4.1.2).
    ApplyBatchResult ApplySelected(IReadOnlyList<ProposedFileChangeViewModel> files, string workingDirectoryPath, bool allowProtectedPaths = false);
}
