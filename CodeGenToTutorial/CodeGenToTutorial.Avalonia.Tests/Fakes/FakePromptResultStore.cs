using CodeGenToTutorial.Core.Contracts.Services;
using CodeGenToTutorial.Core.Models;

namespace CodeGenToTutorial.Avalonia.Tests.Fakes;

public class FakePromptResultStore : IPromptResultStore
{
    public string Tutorial { get; private set; } = string.Empty;

    public string WorkingDirectoryPath { get; private set; } = string.Empty;

    public IReadOnlyList<ProposedFileChange> Files { get; private set; } = Array.Empty<ProposedFileChange>();

    public int SetResultCallCount { get; private set; }

    public void SetResult(string tutorial, IReadOnlyList<ProposedFileChange> files, string workingDirectoryPath)
    {
        SetResultCallCount++;
        Tutorial = tutorial;
        Files = files;
        WorkingDirectoryPath = workingDirectoryPath;
    }
}
