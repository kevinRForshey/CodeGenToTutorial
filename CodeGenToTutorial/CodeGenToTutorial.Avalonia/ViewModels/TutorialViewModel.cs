using System.Collections.ObjectModel;

using CodeGenToTutorial.Avalonia.Contracts.ViewModels;
using CodeGenToTutorial.Core.Contracts.Services;
using CodeGenToTutorial.Core.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeGenToTutorial.Avalonia.ViewModels;

public partial class TutorialViewModel : ObservableRecipient, INavigationAware
{
    private readonly IPromptResultStore _promptResultStore;

    [ObservableProperty]
    private ProposedFileChangeViewModel? selected;

    public ObservableCollection<ProposedFileChangeViewModel> Files { get; } = new();

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

        // The WinUI version only auto-selects the first file once the master/detail view reports it has
        // room to show both panes side by side; the hand-built two-pane layout here always shows both,
        // so it's safe to select eagerly instead of waiting for a ViewStateChanged-equivalent signal.
        EnsureItemSelected();
    }

    public void OnNavigatedFrom()
    {
    }

    public void EnsureItemSelected()
    {
        Selected ??= Files.FirstOrDefault();
    }
}
