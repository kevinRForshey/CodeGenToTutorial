using System.Collections.ObjectModel;

using CodeGenToTutorial.Avalonia.Contracts.Services;
using CodeGenToTutorial.Avalonia.Contracts.ViewModels;
using CodeGenToTutorial.Avalonia.Strings;
using CodeGenToTutorial.Core.Contracts.Services;
using CodeGenToTutorial.Core.Models;
using CodeGenToTutorial.Core.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeGenToTutorial.Avalonia.ViewModels;

public partial class TutorialViewModel : ObservableRecipient, INavigationAware, IConfirmNavigationAway
{
    private readonly IPromptResultStore _promptResultStore;
    private readonly IApplyOrchestrationService _applyOrchestrationService;
    private readonly IApplyHistoryLog _applyHistoryLog;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private ProposedFileChangeViewModel? selected;

    [ObservableProperty]
    private string applyStatusMessage = string.Empty;

    public ObservableCollection<ProposedFileChangeViewModel> Files { get; } = new();

    // T4.2.3: display-ready rows for the apply-history log, newest first. Populated from
    // IApplyHistoryLog rather than bound to it directly, since that store's Entries is a plain
    // IReadOnlyList<ApplyHistoryEntry> with no change notification the view could bind to.
    public ObservableCollection<string> ApplyHistoryEntries { get; } = new();

    // Drives whether the history panel shows itself or stays collapsed - there's nothing useful to
    // show before the first apply of the session.
    public bool HasApplyHistory => ApplyHistoryEntries.Count > 0;

    public TutorialViewModel(IPromptResultStore promptResultStore, IApplyOrchestrationService applyOrchestrationService, IApplyHistoryLog applyHistoryLog, IDialogService dialogService)
    {
        _promptResultStore = promptResultStore;
        _applyOrchestrationService = applyOrchestrationService;
        _applyHistoryLog = applyHistoryLog;
        _dialogService = dialogService;

        // ObservableCollection<T> raises PropertyChanged("Count") on its own, but HasNoFiles/
        // HasFilesButNoneSelected are derived from that count rather than being Count itself, so
        // the view's bindings to them need an explicit nudge whenever the collection changes.
        Files.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasNoFiles));
            OnPropertyChanged(nameof(HasFilesButNoneSelected));
        };

        ApplyHistoryEntries.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasApplyHistory));
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

        // The log is session-scoped (same singleton across page visits), so re-read it every time
        // this page is shown rather than only right after an apply/undo/revert - otherwise
        // navigating away and back would show a stale list.
        RefreshApplyHistoryEntries();
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
        RefreshApplyHistoryEntries();

        await _dialogService.InfoAsync(
            "Tutorial_ApplySelectedResultTitle".GetLocalized(),
            ApplyStatusMessage,
            "Tutorial_ApplySelectedResultClose".GetLocalized());
    }

    // T4.2.2: reverts the files touched by the most recent ApplySelected batch to their content
    // from immediately before that batch, confirming first since it overwrites whatever's on disk
    // now. Nothing-to-undo is reported the same way ApplySelectedAsync reports nothing-selected -
    // inline via ApplyStatusMessage - rather than disabling the command, consistent with how this
    // page already handles that case.
    [RelayCommand]
    private async Task UndoLastApplyAsync()
    {
        var confirmed = await _dialogService.ConfirmAsync(
            "Tutorial_UndoLastApplyConfirmTitle".GetLocalized(),
            "Tutorial_UndoLastApplyConfirmMessage".GetLocalized(),
            "Tutorial_UndoLastApplyConfirmButton".GetLocalized(),
            "Tutorial_UndoLastApplyCancelButton".GetLocalized());

        if (!confirmed)
        {
            return;
        }

        if (!_applyOrchestrationService.TryUndoLastApply(_promptResultStore.WorkingDirectoryPath, out var result))
        {
            ApplyStatusMessage = "Tutorial_UndoLastApplyNothingToUndo".GetLocalized();
            return;
        }

        RefreshFilesAffectedBy(result);
        ApplyStatusMessage = BuildRevertStatusMessage(result);
        RefreshApplyHistoryEntries();

        await _dialogService.InfoAsync(
            "Tutorial_UndoLastApplyResultTitle".GetLocalized(),
            ApplyStatusMessage,
            "Tutorial_UndoLastApplyResultClose".GetLocalized());
    }

    // T4.2.2: reverts every file this session has applied a change to, back to its content from
    // before this session touched it at all - not just the most recent batch, so this is confirmed
    // separately and more strongly worded than UndoLastApplyAsync.
    [RelayCommand]
    private async Task RevertAllAppliedAsync()
    {
        var confirmed = await _dialogService.ConfirmAsync(
            "Tutorial_RevertAllAppliedConfirmTitle".GetLocalized(),
            "Tutorial_RevertAllAppliedConfirmMessage".GetLocalized(),
            "Tutorial_RevertAllAppliedConfirmButton".GetLocalized(),
            "Tutorial_RevertAllAppliedCancelButton".GetLocalized());

        if (!confirmed)
        {
            return;
        }

        var result = _applyOrchestrationService.RevertAllApplied(_promptResultStore.WorkingDirectoryPath);

        if (result.Results.Count == 0)
        {
            ApplyStatusMessage = "Tutorial_RevertAllAppliedNothingToRevert".GetLocalized();
            return;
        }

        RefreshFilesAffectedBy(result);
        ApplyStatusMessage = BuildRevertStatusMessage(result);
        RefreshApplyHistoryEntries();

        await _dialogService.InfoAsync(
            "Tutorial_RevertAllAppliedResultTitle".GetLocalized(),
            ApplyStatusMessage,
            "Tutorial_RevertAllAppliedResultClose".GetLocalized());
    }

    private string BuildRevertStatusMessage(RevertResult result)
    {
        var messageParts = new List<string> { string.Format("Tutorial_RevertResultSucceeded".GetLocalized(), result.SucceededCount) };

        if (result.FailedCount > 0)
        {
            var failedPaths = string.Join(", ", result.FailedFiles.Select(r => r.FilePath));
            messageParts.Add(string.Format("Tutorial_RevertResultFailures".GetLocalized(), result.FailedCount, failedPaths));
        }

        return string.Join(" ", messageParts);
    }

    // Writes back by TryUndoLastApply/RevertAllApplied happen directly against disk, with no
    // reference to the live ProposedFileChangeViewModel instances this page is showing - so each
    // successfully reverted file's OriginalContent/hunks need an explicit refresh afterward, the
    // same re-diff RefreshOriginalContent already does for a T3.3.3 conflict.
    private void RefreshFilesAffectedBy(RevertResult result)
    {
        var revertedPaths = result.Results.Where(r => r.Succeeded).Select(r => r.FilePath).ToHashSet();

        foreach (var file in Files.Where(f => revertedPaths.Contains(f.FilePath)))
        {
            file.RefreshOriginalContent(_promptResultStore.WorkingDirectoryPath);
        }
    }

    // T4.2.3: re-reads IApplyHistoryLog.Entries (oldest first) and rebuilds the display list
    // newest first, so the most recent apply/undo/revert always shows at the top.
    private void RefreshApplyHistoryEntries()
    {
        ApplyHistoryEntries.Clear();

        foreach (var entry in _applyHistoryLog.Entries.Reverse())
        {
            ApplyHistoryEntries.Add(FormatHistoryEntry(entry));
        }
    }

    private static string FormatHistoryEntry(ApplyHistoryEntry entry)
    {
        var timestamp = entry.Timestamp.ToLocalTime().ToString("g");
        var key = entry.Kind switch
        {
            ApplyHistoryEntryKind.Applied => "Tutorial_HistoryEntryApplied",
            ApplyHistoryEntryKind.UndoLastApply => "Tutorial_HistoryEntryUndoLastApply",
            ApplyHistoryEntryKind.RevertAllApplied => "Tutorial_HistoryEntryRevertAllApplied",
            _ => throw new ArgumentOutOfRangeException(nameof(entry), entry.Kind, "Unknown apply-history entry kind."),
        };

        return string.Format(key.GetLocalized(), timestamp, entry.FileCount);
    }
}
