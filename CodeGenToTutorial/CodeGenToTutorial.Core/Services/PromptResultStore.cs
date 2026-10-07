using CodeGenToTutorial.Core.Contracts.Services;
using CodeGenToTutorial.Core.ViewModels;

namespace CodeGenToTutorial.Core.Services;

// In-memory hand-off between PromptView (produces results) and DiffsView (displays them). Both view models are
// transient, so this singleton is what lets the parsed results survive navigation between the two pages.
public class PromptResultStore : IPromptResultStore
{
    public string Tutorial { get; private set; } = string.Empty;

    public string WorkingDirectoryPath { get; private set; } = string.Empty;

    public IReadOnlyList<ProposedFileChangeViewModel> Files { get; private set; } = Array.Empty<ProposedFileChangeViewModel>();

    public void SetResult(string tutorial, IReadOnlyList<ProposedFileChangeViewModel> files, string workingDirectoryPath)
    {
        Tutorial = tutorial;
        Files = files;
        WorkingDirectoryPath = workingDirectoryPath;
    }
}
