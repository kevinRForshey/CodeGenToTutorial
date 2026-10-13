using CodeGenToTutorial.Core.Contracts.Services;
using CodeGenToTutorial.Core.Models;
using CodeGenToTutorial.Core.ViewModels;

namespace CodeGenToTutorial.Avalonia.Tests.Fakes;

public class FakeApplyOrchestrationService : IApplyOrchestrationService
{
    public ApplyPreview PreviewToReturn { get; set; } = new(0, 0);

    public ApplyBatchResult ApplySelectedResultToReturn { get; set; } = new(Array.Empty<FileApplyResult>());

    public int PreviewCallCount { get; private set; }

    public int ApplySelectedCallCount { get; private set; }

    public string? LastWorkingDirectoryPath { get; private set; }

    public bool? LastAllowProtectedPaths { get; private set; }

    public ApplyPreview Preview(IReadOnlyList<ProposedFileChangeViewModel> files, string workingDirectoryPath)
    {
        PreviewCallCount++;
        return PreviewToReturn;
    }

    public ApplyBatchResult ApplySelected(IReadOnlyList<ProposedFileChangeViewModel> files, string workingDirectoryPath, bool allowProtectedPaths = false)
    {
        ApplySelectedCallCount++;
        LastWorkingDirectoryPath = workingDirectoryPath;
        LastAllowProtectedPaths = allowProtectedPaths;
        return ApplySelectedResultToReturn;
    }
}
