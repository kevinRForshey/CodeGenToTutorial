using CodeGenToTutorial.Contracts.Services;
using CodeGenToTutorial.Models;

namespace CodeGenToTutorial.Services;

// In-memory hand-off between PromptPage (produces results) and DiffsPage (displays them). Both view models are
// transient, so this singleton is what lets the parsed results survive navigation between the two pages.
public class PromptResultStore : IPromptResultStore
{
    public string Tutorial { get; private set; } = string.Empty;

    public string WorkingDirectoryPath { get; private set; } = string.Empty;

    public IReadOnlyList<ProposedFileChange> Files { get; private set; } = Array.Empty<ProposedFileChange>();

    public void SetResult(string tutorial, IReadOnlyList<ProposedFileChange> files, string workingDirectoryPath)
    {
        Tutorial = tutorial;
        Files = files;
        WorkingDirectoryPath = workingDirectoryPath;
    }
}
