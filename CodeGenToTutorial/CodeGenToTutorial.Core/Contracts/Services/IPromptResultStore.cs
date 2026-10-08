using System.Collections.ObjectModel;
using System.ComponentModel;

using CodeGenToTutorial.Core.ViewModels;

namespace CodeGenToTutorial.Core.Contracts.Services;

// INotifyPropertyChanged (Tutorial/WorkingDirectoryPath) plus the ObservableCollection on Files
// (CollectionChanged) are what let a consumer react to a new prompt result without having to be
// re-navigated to - today only PromptViewModel.OnNavigatedTo/Files pulls from this store on a
// navigation lifecycle event, but the store itself is a singleton that outlives any one page, so
// anything bound directly to it (e.g. a persistent tutorial-summary header) stays in sync.
public interface IPromptResultStore : INotifyPropertyChanged
{
    string Tutorial { get; }

    string WorkingDirectoryPath { get; }

    ObservableCollection<ProposedFileChangeViewModel> Files { get; }

    void SetResult(string tutorial, IReadOnlyList<ProposedFileChangeViewModel> files, string workingDirectoryPath);
}
