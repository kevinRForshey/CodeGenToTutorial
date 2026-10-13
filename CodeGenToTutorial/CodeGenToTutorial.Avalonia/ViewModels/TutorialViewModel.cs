using System.Collections.ObjectModel;

using CodeGenToTutorial.Avalonia.Contracts.Services;
using CodeGenToTutorial.Avalonia.Contracts.ViewModels;
using CodeGenToTutorial.Avalonia.Strings;
using CodeGenToTutorial.Core.Contracts.Services;
using CodeGenToTutorial.Core.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeGenToTutorial.Avalonia.ViewModels;

public partial class TutorialViewModel : ObservableRecipient, INavigationAware, IConfirmNavigationAway
{
    private readonly IPromptResultStore _promptResultStore;
    private readonly IApplyOrchestrationService _applyOrchestrationService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private ProposedFileChangeViewModel? selected;

    [ObservableProperty]
    private string applyStatusMessage = string.Empty;

    public ObservableCollection<ProposedFileChangeViewModel> Files { get; } = new();

    public TutorialViewModel(IPromptResultStore promptResultStore, IApplyOrchestrationService applyOrchestrationService, IDialogService dialogService)
    {
        _promptResultStore = promptResultStore;
        _applyOrchestrationService = applyOrchestrationService;
        _dialogService = dialogService;

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

    public bool HasUnsavedChanges => Files.Any(f => f.IsDirty);

    // Drives the Tutorial page's empty-state messaging: with no files at all (no prompt run yet)
    // the master list is empty and there's nothing to select, which otherwise looks indistinguishable
    // from the page having failed to load.
    public bool HasNoFiles => Files.Count == 0;

    public bool HasFilesButNoneSelected => Files.Count > 0 && Selected == null;

    partial void OnSelectedChanged(ProposedFileChangeViewModel? value) => OnPropertyChanged(nameof(HasFilesButNoneSelected));

    public void EnsureItemSelected()
    {
        Selected ??= Files.FirstOrDefault();
    }

    // F3.3: applies every selected hunk/whole-file change across every file in the tutorial, not
    // just the one file currently shown in the detail pane. Confirms with a pre-apply summary
    // (T3.3.1) before touching disk, then reports what happened per file (T3.3.3 conflicts,
    // T3.3.4 partial failures) rather than assuming the whole batch succeeded.
    [RelayCommand]
    private async Task ApplySelectedAsync()
    {
        var preview = _applyOrchestrationService.Preview(Files, _promptResultStore.WorkingDirectoryPath);

        if (preview.FileCount == 0)
        {
            ApplyStatusMessage = "Tutorial_ApplySelectedNoneSelected".GetLocalized();
            return;
        }

        var confirmed = await _dialogService.ConfirmAsync(
            "Tutorial_ApplySelectedConfirmTitle".GetLocalized(),
            string.Format("Tutorial_ApplySelectedConfirmMessage".GetLocalized(), preview.ChangeCount, preview.FileCount),
            "Tutorial_ApplySelectedConfirmButton".GetLocalized(),
            "Tutorial_ApplySelectedCancelButton".GetLocalized());

        if (!confirmed)
        {
            return;
        }

        // Asked separately from the general apply-confirm above (T4.1.2), so declining it only
        // skips the protected files (reported below as ProtectedPathBlockedCount) rather than
        // aborting the whole batch - consistent with T3.3.4's "one file's problem never stops the
        // rest" philosophy.
        var allowProtectedPaths = true;

        if (preview.ProtectedPathCount > 0)
        {
            var protectedPaths = string.Join(", ", preview.ProtectedPathFiles);
            allowProtectedPaths = await _dialogService.ConfirmAsync(
                "Tutorial_ApplyProtectedPathsConfirmTitle".GetLocalized(),
                string.Format("Tutorial_ApplyProtectedPathsConfirmMessage".GetLocalized(), preview.ProtectedPathCount, protectedPaths),
                "Tutorial_ApplyProtectedPathsConfirmButton".GetLocalized(),
                "Tutorial_ApplyProtectedPathsConfirmCancelButton".GetLocalized());
        }

        var result = _applyOrchestrationService.ApplySelected(Files, _promptResultStore.WorkingDirectoryPath, allowProtectedPaths);

        var messageParts = new List<string> { string.Format("Tutorial_ApplySelectedResultApplied".GetLocalized(), result.AppliedCount) };

        if (result.ConflictCount > 0)
        {
            var conflictedPaths = string.Join(", ", result.ConflictedFiles.Select(r => r.FilePath));
            messageParts.Add(string.Format("Tutorial_ApplySelectedResultConflicts".GetLocalized(), result.ConflictCount, conflictedPaths));
        }

        if (result.FailedCount > 0)
        {
            var failedPaths = string.Join(", ", result.FailedFiles.Select(r => r.FilePath));
            messageParts.Add(string.Format("Tutorial_ApplySelectedResultFailures".GetLocalized(), result.FailedCount, failedPaths));
        }

        if (result.ProtectedPathBlockedCount > 0)
        {
            var blockedPaths = string.Join(", ", result.ProtectedPathBlockedFiles.Select(r => r.FilePath));
            messageParts.Add(string.Format("Tutorial_ApplySelectedResultProtectedBlocked".GetLocalized(), result.ProtectedPathBlockedCount, blockedPaths));
        }

        ApplyStatusMessage = string.Join(" ", messageParts);

        await _dialogService.InfoAsync(
            "Tutorial_ApplySelectedResultTitle".GetLocalized(),
            ApplyStatusMessage,
            "Tutorial_ApplySelectedResultClose".GetLocalized());
    }
}
