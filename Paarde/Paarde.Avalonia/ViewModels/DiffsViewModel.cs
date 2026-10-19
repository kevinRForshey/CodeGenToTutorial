using System.Collections.ObjectModel;

using Paarde.Avalonia.Contracts.Services;
using Paarde.Avalonia.Contracts.ViewModels;
using Paarde.Avalonia.Models;
using Paarde.Core.Contracts.Services;
using Paarde.Core.Models;
using Paarde.Core.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Paarde.Avalonia.ViewModels;

public partial class DiffsViewModel : ObservableRecipient, INavigationAware
{
    private readonly IPromptResultStore _promptResultStore;
    private readonly INavigationService _navigationService;

    // T8.4.5: the hunk DiffsDetailControl should scroll to once it's materialized for the file a
    // DiffJumpRequest selected - "take once" (read and cleared by DiffsDetailControl.axaml.cs) so a
    // later, unrelated re-materialization of the same control never re-triggers the scroll.
    private DiffHunk? _pendingScrollTarget;

    [ObservableProperty]
    private ProposedFileChangeViewModel? selected;

    // T8.4.5: the step a DiffJumpRequest jumped from, if this page was reached that way - drives
    // the "Back to tutorial" button. Null for an ordinary navigation to this page.
    [ObservableProperty]
    private ProposedHunk? returnToStep;

    public ObservableCollection<ProposedFileChangeViewModel> Files { get; } = new();

    public DiffsViewModel(IPromptResultStore promptResultStore, INavigationService navigationService)
    {
        _promptResultStore = promptResultStore;
        _navigationService = navigationService;

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

        // T8.4.5: a jump from the Tutorial page selects its file directly and remembers both the
        // hunk to scroll to and the step to return to, rather than relying on EnsureItemSelected's
        // first-file default below.
        if (parameter is DiffJumpRequest jump)
        {
            _pendingScrollTarget = jump.TargetHunk;
            ReturnToStep = jump.OriginStep;
            Selected = jump.File;
        }
        else
        {
            ReturnToStep = null;
        }

        // The WinUI version only auto-selects the first file once the master/detail view reports it has
        // room to show both panes side by side; the hand-built two-pane layout here always shows both,
        // so it's safe to select eagerly instead of waiting for a ViewStateChanged-equivalent signal.
        EnsureItemSelected();
    }

    public void OnNavigatedFrom()
    {
    }

    // "Take once" accessor for the scroll target a DiffJumpRequest set - called by
    // DiffsDetailControl.axaml.cs once it's materialized for the target file.
    public DiffHunk? TakePendingScrollTarget()
    {
        var target = _pendingScrollTarget;
        _pendingScrollTarget = null;
        return target;
    }

    // T8.4.5: the reverse of a tutorial step's "View in Diff" jump.
    [RelayCommand(CanExecute = nameof(CanGoBackToTutorial))]
    private void BackToTutorial() =>
        _navigationService.NavigateTo(typeof(TutorialViewModel).FullName!, new TutorialJumpRequest(Selected!, ReturnToStep!));

    private bool CanGoBackToTutorial() => ReturnToStep is not null && Selected is not null;

    partial void OnReturnToStepChanged(ProposedHunk? value) => BackToTutorialCommand.NotifyCanExecuteChanged();

    // Drives the Diffs page's empty-state messaging: with no files at all (no prompt run yet) the
    // master list is empty and there's nothing to select, which otherwise looks indistinguishable
    // from the page having failed to load.
    public bool HasNoFiles => Files.Count == 0;

    public bool HasFilesButNoneSelected => Files.Count > 0 && Selected == null;

    partial void OnSelectedChanged(ProposedFileChangeViewModel? value)
    {
        OnPropertyChanged(nameof(HasFilesButNoneSelected));
        BackToTutorialCommand.NotifyCanExecuteChanged();

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
