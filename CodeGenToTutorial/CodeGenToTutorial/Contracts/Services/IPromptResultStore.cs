using CodeGenToTutorial.Models;

namespace CodeGenToTutorial.Contracts.Services;

public interface IPromptResultStore
{
    string Tutorial { get; }

    IReadOnlyList<ProposedFileChange> Files { get; }

    void SetResult(string tutorial, IReadOnlyList<ProposedFileChange> files);
}
