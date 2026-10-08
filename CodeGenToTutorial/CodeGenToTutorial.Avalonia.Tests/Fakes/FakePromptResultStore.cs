using System.Collections.ObjectModel;
using System.ComponentModel;

using CodeGenToTutorial.Core.Contracts.Services;
using CodeGenToTutorial.Core.ViewModels;

namespace CodeGenToTutorial.Avalonia.Tests.Fakes;

public class FakePromptResultStore : IPromptResultStore
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Tutorial { get; private set; } = string.Empty;

    public string WorkingDirectoryPath { get; private set; } = string.Empty;

    public ObservableCollection<ProposedFileChangeViewModel> Files { get; } = new();

    public int SetResultCallCount { get; private set; }

    public void SetResult(string tutorial, IReadOnlyList<ProposedFileChangeViewModel> files, string workingDirectoryPath)
    {
        SetResultCallCount++;
        Tutorial = tutorial;
        WorkingDirectoryPath = workingDirectoryPath;

        Files.Clear();
        foreach (var file in files)
        {
            Files.Add(file);
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
}
