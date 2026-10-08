using System.Collections.ObjectModel;

using CodeGenToTutorial.Core.Contracts.Services;
using CodeGenToTutorial.Core.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeGenToTutorial.Core.Services;

// In-memory hand-off between PromptView (produces results) and DiffsView/TutorialView (display them). Both
// consuming view models are transient, so this singleton is what lets the parsed results survive navigation
// between pages. Tutorial/WorkingDirectoryPath are ObservableObject properties and Files is a fixed
// ObservableCollection instance mutated in place (not reassigned) on SetResult, so anything subscribed to
// PropertyChanged/CollectionChanged - not just a page re-reading the store from OnNavigatedTo - sees a new
// prompt result as soon as it lands.
public partial class PromptResultStore : ObservableObject, IPromptResultStore
{
    [ObservableProperty]
    private string tutorial = string.Empty;

    [ObservableProperty]
    private string workingDirectoryPath = string.Empty;

    public ObservableCollection<ProposedFileChangeViewModel> Files { get; } = new();

    public void SetResult(string tutorial, IReadOnlyList<ProposedFileChangeViewModel> files, string workingDirectoryPath)
    {
        Tutorial = tutorial;
        WorkingDirectoryPath = workingDirectoryPath;

        Files.Clear();
        foreach (var file in files)
        {
            Files.Add(file);
        }
    }
}
