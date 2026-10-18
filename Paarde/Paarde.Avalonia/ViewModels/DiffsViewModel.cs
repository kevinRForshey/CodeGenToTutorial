using System.Collections.ObjectModel;

using Paarde.Avalonia.Contracts.ViewModels;
using Paarde.Core.Contracts.Services;
using Paarde.Core.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;

namespace Paarde.Avalonia.ViewModels;

public partial class DiffsViewModel : ObservableRecipient, INavigationAware
{
    private readonly IPromptResultStore _promptResultStore;

    [ObservableProperty]
    private ProposedFileChangeViewModel? selected;

    public ObservableCollection<ProposedFileChangeViewModel> Files { get; } = new();

    public DiffsViewModel(IPromptResultStore promptResultStore)
    {
        _promptResultStore = promptResultStore;

        // ObservableCollection<T> raises PropertyChanged("Count") on its own, but HasNoFiles/
        // HasFilesButNoneSelected are derived from that count rather than being Count itself, so
        // the view's bindings to them need an explicit nudge whenever the collection changes.
        Files.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasNoFiles));
            OnPropertyChanged(nameof(HasFilesButNoneSelected));
        };
    }

    public void OnNavigatedTo(object parameter)
    {
        Files.Clear();

        // Reuse the store's own instances - the same ones the Tutorial page edits - rather than
        // building copies, so DiffLines (recomputed inside ProposedFileChangeViewModel) reflects the
        // user's live edits instead of a snapshot of the raw proposal taken at navigation time.
        // Reading each file's on-disk content and recomputing its diff is deferred to selection
        // (see OnSelectedChanged) rather than done eagerly here for every file - with real file
        // content (as opposed to the empty-original diff computed at parse time) that's a disk read
        // plus an O(n*m) LCS pass per file, which done for every file up front froze the UI while
        // navigating to this page even though only one file is ever shown at a time.
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

    // Drives the Diffs page's empty-state messaging: with no files at all (no prompt run yet) the
    // master list is empty and there's nothing to select, which otherwise looks indistinguishable
    // from the page having failed to load.
    public bool HasNoFiles => Files.Count == 0;

    public bool HasFilesButNoneSelected => Files.Count > 0 && Selected == null;

    partial void OnSelectedChanged(ProposedFileChangeViewModel? value)
    {
        OnPropertyChanged(nameof(HasFilesButNoneSelected));

        // Loading the on-disk original content (and the real diff it triggers via
        // OnOriginalContentChanged) is deferred until a file is actually shown, rather than done for
        // every file in OnNavigatedTo, so the UI thread only ever pays that cost for the one file
        // the user is looking at. RefreshOriginalContent (Core) is the same re-diff-from-disk logic
        // ApplyOrchestrationService's conflict handling (T3.3.3) offers after a write is skipped.
        value?.RefreshOriginalContent(_promptResultStore.WorkingDirectoryPath);
    }

    public void EnsureItemSelected()
    {
        Selected ??= Files.FirstOrDefault();
    }
}
