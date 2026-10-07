using CodeGenToTutorial.Core.Contracts.Services;
using CodeGenToTutorial.Core.ViewModels;

namespace CodeGenToTutorial.Avalonia.Tests.Fakes;

public class FakePromptResultStore : IPromptResultStore
{
    public string Tutorial { get; private set; } = string.Empty;

    public string WorkingDirectoryPath { get; private set; } = string.Empty;

    public IReadOnlyList<ProposedFileChangeViewModel> Files { get; private set; } = Array.Empty<ProposedFileChangeViewModel>();

    public int SetResultCallCount { get; private set; }

    public void SetResult(string tutorial, IReadOnlyList<ProposedFileChangeViewModel> files, string workingDirectoryPath)
    {
        SetResultCallCount++;
        Tutorial = tutorial;
        Files = files;
        WorkingDirectoryPath = workingDirectoryPath;
    }
}
