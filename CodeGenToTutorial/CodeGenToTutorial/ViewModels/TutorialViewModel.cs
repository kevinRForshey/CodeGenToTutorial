using System.Collections.ObjectModel;

using CodeGenToTutorial.Contracts.Services;
using CodeGenToTutorial.Contracts.ViewModels;
using CodeGenToTutorial.Models;

using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeGenToTutorial.ViewModels;

public partial class TutorialViewModel : ObservableRecipient, INavigationAware
{
    private readonly IPromptResultStore _promptResultStore;

    [ObservableProperty]
    private ProposedFileChange? selected;

    public ObservableCollection<ProposedFileChange> Files { get; } = new();

    public TutorialViewModel(IPromptResultStore promptResultStore)
    {
        _promptResultStore = promptResultStore;
    }

    public void OnNavigatedTo(object parameter)
    {
        Files.Clear();

        foreach (var file in _promptResultStore.Files)
        {
            Files.Add(file);
        }

        Selected = null;
    }

    public void OnNavigatedFrom()
    {
    }

    public void EnsureItemSelected()
    {
        Selected ??= Files.FirstOrDefault();
    }
}
