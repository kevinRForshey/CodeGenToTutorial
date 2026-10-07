using CodeGenToTutorial.Core.ViewModels;

namespace CodeGenToTutorial.Core.Contracts.Services;

public interface IPromptResultStore
{
    string Tutorial { get; }

    string WorkingDirectoryPath { get; }

    IReadOnlyList<ProposedFileChangeViewModel> Files { get; }

    void SetResult(string tutorial, IReadOnlyList<ProposedFileChangeViewModel> files, string workingDirectoryPath);
}
