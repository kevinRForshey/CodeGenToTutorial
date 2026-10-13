using CodeGenToTutorial.Core.Models;
using CodeGenToTutorial.Core.ViewModels;

namespace CodeGenToTutorial.Core.Contracts.Services;

public interface IApplyOrchestrationService
{
    // Cheap, I/O-free summary for a confirmation prompt before ApplySelected actually touches disk.
    ApplyPreview Preview(IReadOnlyList<ProposedFileChangeViewModel> files);

    // Applies every file that has something selected, independently of the others - one file's
    // conflict or failure never stops the rest of the batch (T3.3.4).
    ApplyBatchResult ApplySelected(IReadOnlyList<ProposedFileChangeViewModel> files, string workingDirectoryPath);
}
