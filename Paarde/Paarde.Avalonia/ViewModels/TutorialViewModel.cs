using System.Collections.ObjectModel;

using Paarde.Avalonia.Contracts.Services;
using Paarde.Avalonia.Contracts.ViewModels;
using Paarde.Avalonia.Models;
using Paarde.Avalonia.Strings;
using Paarde.Core.Contracts.Services;
using Paarde.Core.Helpers;
using Paarde.Core.Models;
using Paarde.Core.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Paarde.Avalonia.ViewModels;

public partial class TutorialViewModel : ObservableRecipient, INavigationAware, IConfirmNavigationAway
{
    private readonly IPromptResultStore _promptResultStore;
    private readonly IApplyOrchestrationService _applyOrchestrationService;
    private readonly IApplyHistoryLog _applyHistoryLog;
    private readonly IDialogService _dialogService;
    private readonly ILlmRunner _llmRunner;
    private readonly IPromptTemplateProvider _promptTemplateProvider;
    private readonly IFileSavePickerService _fileSavePickerService;

    // T8.4.5: the step TutorialDetailControl should scroll to once it's materialized for the file a
    // TutorialJumpRequest selected - "take once" (read and cleared by TutorialDetailControl.axaml.cs)
    // so a later, unrelated re-materialization of the same control never re-triggers the scroll.
    private ProposedHunk? _pendingStepTarget;

    // T8.4.1: the `## Tutorial` overview PromptViewModel parses into PromptResultStore.Tutorial but
    // no view previously read - pulled on navigation the same way Files already is, rather than a
    // live PropertyChanged subscription, since this page already re-reads the whole store on every
    // OnNavigatedTo.
    [ObservableProperty]
    private string tutorial = string.Empty;

    [ObservableProperty]
    private ProposedFileChangeViewModel? selected;

    [ObservableProperty]
    private string applyStatusMessage = string.Empty;

    // T7.3.2: the scoped "refine this file" follow-up - a resumed-session request to change only
    // Selected, reusing the same instruction template/parsing pipeline as the main run rather than
    // a bespoke one.
    [ObservableProperty]
    private string refineInstruction = string.Empty;

    [ObservableProperty]
    private string refineStatusMessage = string.Empty;

    [ObservableProperty]
    private bool isRefining;

    // T8.4.4: status for the export-to-file commands below, surfaced the same inline way as
    // ApplyStatusMessage rather than a dialog - exporting isn't a destructive/multi-outcome
    // operation worth interrupting the user for.
    [ObservableProperty]
    private string exportStatusMessage = string.Empty;

    public ObservableCollection<ProposedFileChangeViewModel> Files { get; } = new();

    // T4.2.3: display-ready rows for the apply-history log, newest first. Populated from
    // IApplyHistoryLog rather than bound to it directly, since that store's Entries is a plain
    // IReadOnlyList<ApplyHistoryEntry> with no change notification the view could bind to.
    public ObservableCollection<string> ApplyHistoryEntries { get; } = new();

    // Drives whether the history panel shows itself or stays collapsed - there's nothing useful to
    // show before the first apply of the session.
    public bool HasApplyHistory => ApplyHistoryEntries.Count > 0;

    public TutorialViewModel(IPromptResultStore promptResultStore, IApplyOrchestrationService applyOrchestrationService, IApplyHistoryLog applyHistoryLog, IDialogService dialogService, ILlmRunner llmRunner, IPromptTemplateProvider promptTemplateProvider, IFileSavePickerService fileSavePickerService)
    {
        _promptResultStore = promptResultStore;
        _applyOrchestrationService = applyOrchestrationService;
        _applyHistoryLog = applyHistoryLog;
        _dialogService = dialogService;
        _llmRunner = llmRunner;
        _promptTemplateProvider = promptTemplateProvider;
        _fileSavePickerService = fileSavePickerService;

        // ObservableCollection<T> raises PropertyChanged("Count") on its own, but HasNoFiles/
        // HasFilesButNoneSelected are derived from that count rather than being Count itself, so
        // the view's bindings to them need an explicit nudge whenever the collection changes.
        Files.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasNoFiles));
            OnPropertyChanged(nameof(HasFilesButNoneSelected));
            ExportTutorialAsMarkdownCommand.NotifyCanExecuteChanged();
            ExportTutorialAsHtmlCommand.NotifyCanExecuteChanged();
        };

        ApplyHistoryEntries.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasApplyHistory));
    }

    public void OnNavigatedTo(object parameter)
    {
        Tutorial = _promptResultStore.Tutorial;

        Files.Clear();

        foreach (var file in _promptResultStore.Files)
        {
            Files.Add(file);
        }

        // T8.4.5: a jump from the Diffs page's "Back to tutorial" selects its file directly and
        // remembers which step to scroll to, rather than relying on EnsureItemSelected's first-file
        // default below.
        if (parameter is TutorialJumpRequest jump)
        {
            _pendingStepTarget = jump.TargetStep;
            Selected = jump.File;
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

    // Drives whether the tutorial-overview header shows itself - a response with no files (or a
    // plain-text/unstructured response) can still have a non-empty overview worth showing.
    public bool HasTutorialOverview => !string.IsNullOrWhiteSpace(Tutorial);

    partial void OnTutorialChanged(string value)
    {
        OnPropertyChanged(nameof(HasTutorialOverview));
        ExportTutorialAsMarkdownCommand.NotifyCanExecuteChanged();
        ExportTutorialAsHtmlCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedChanged(ProposedFileChangeViewModel? value)
    {
        OnPropertyChanged(nameof(HasFilesButNoneSelected));
        RefineSelectedFileCommand.NotifyCanExecuteChanged();
    }

    partial void OnRefineInstructionChanged(string value) => RefineSelectedFileCommand.NotifyCanExecuteChanged();

    partial void OnIsRefiningChanged(bool value) => RefineSelectedFileCommand.NotifyCanExecuteChanged();

    public void EnsureItemSelected()
    {
        Selected ??= Files.FirstOrDefault();
    }

    // "Take once" accessor for the step target a TutorialJumpRequest set - called by
    // TutorialDetailControl.axaml.cs once it's materialized for the target file.
    public ProposedHunk? TakePendingStepTarget()
    {
        var target = _pendingStepTarget;
        _pendingStepTarget = null;
        return target;
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

        // Asked separately from the general apply-confirm above (T4.3.2): unlike protected paths,
        // overwriting a file that's already dirty in git isn't something a per-file skip can make
        // safe - there's no commit to fall back on for it either way - so declining here cancels
        // the whole apply rather than just excluding those files from the batch.
        if (preview.IsGitRepository && preview.GitDirtyFileCount > 0)
        {
            var dirtyPaths = string.Join(", ", preview.GitDirtyFiles);
            var proceedDespiteDirtyFiles = await _dialogService.ConfirmAsync(
                "Tutorial_ApplyGitDirtyConfirmTitle".GetLocalized(),
                string.Format("Tutorial_ApplyGitDirtyConfirmMessage".GetLocalized(), preview.GitDirtyFileCount, dirtyPaths),
                "Tutorial_ApplyGitDirtyConfirmButton".GetLocalized(),
                "Tutorial_ApplyGitDirtyConfirmCancelButton".GetLocalized());

            if (!proceedDespiteDirtyFiles)
            {
                return;
            }
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

    // T7.3.2: a scoped follow-up that only asks Claude to change Selected, resuming the conversation
    // that produced the current result (T7.3.1) so it already has the whole tutorial as context.
    // Reuses the main run's instruction template/JSON contract and StructuredResponseParser rather
    // than a bespoke prompt/parse path, so a successful response is just another file in the same
    // shape Files already holds - deliberately no schema-validation retry loop here (unlike the main
    // run): a failed attempt just reports an error and the user can click the button again.
    [RelayCommand(CanExecute = nameof(CanRefineSelectedFile))]
    private async Task RefineSelectedFileAsync()
    {
        var file = Selected!;
        IsRefining = true;
        RefineStatusMessage = string.Format("Tutorial_RefineRunning".GetLocalized(), file.FilePath);

        try
        {
            var prompt = _promptTemplateProvider.GetTutorialInstructionPrompt() +
                $"\n\nOnly modify the file \"{file.FilePath}\" - do not include any other file in your response. Its current content is:\n{file.EditedContent}" +
                $"\n\nApply this refinement: {RefineInstruction}" +
                $"\n\nThe working directory is: {_promptResultStore.WorkingDirectoryPath}";

            var result = await _llmRunner.RunPromptAsync(prompt, _promptResultStore.WorkingDirectoryPath, resumeSessionId: _promptResultStore.SessionId);

            if (!result.Success)
            {
                RefineStatusMessage = string.Format("Tutorial_RefineFailed".GetLocalized(), file.FilePath, result.ErrorMessage);
                return;
            }

            var parseResult = StructuredResponseParser.Parse(result.Output, _promptResultStore.WorkingDirectoryPath);
            var refinedFile = parseResult.Response.Files.FirstOrDefault(f => f.FilePath == file.FilePath);

            if (parseResult.UsedFallback || refinedFile is null)
            {
                RefineStatusMessage = string.Format("Tutorial_RefineNoMatchingFile".GetLocalized(), file.FilePath);
                return;
            }

            // Content-mode files (unlike edit-mode) come back with OriginalContent unset - see
            // ClaudeJsonResponseParser.MapContentModeFile - so this re-diffs against disk the same
            // way DiffsViewModel.OnSelectedChanged does for every file it shows.
            refinedFile.RefreshOriginalContent(_promptResultStore.WorkingDirectoryPath);
            ReplaceFile(file, refinedFile);

            RefineInstruction = string.Empty;
            RefineStatusMessage = string.Format("Tutorial_RefineSucceeded".GetLocalized(), refinedFile.FilePath);
        }
        finally
        {
            IsRefining = false;
        }
    }

    private bool CanRefineSelectedFile() => Selected is not null && !string.IsNullOrWhiteSpace(RefineInstruction) && !IsRefining;

    // T8.4.4: builds a self-contained document (TutorialExporter, Core) a user can save and share
    // outside the app - Markdown and HTML share this one picker-then-write flow, differing only in
    // which exporter/file-type/extension they pass in.
    [RelayCommand(CanExecute = nameof(CanExportTutorial))]
    private Task ExportTutorialAsMarkdownAsync() =>
        ExportTutorialAsync("Tutorial_ExportAsMarkdownTitle".GetLocalized(), "tutorial.md", "Markdown", "md", TutorialExporter.BuildMarkdown);

    [RelayCommand(CanExecute = nameof(CanExportTutorial))]
    private Task ExportTutorialAsHtmlAsync() =>
        ExportTutorialAsync("Tutorial_ExportAsHtmlTitle".GetLocalized(), "tutorial.html", "HTML", "html", TutorialExporter.BuildHtml);

    private bool CanExportTutorial() => Files.Count > 0 || HasTutorialOverview;

    private async Task ExportTutorialAsync(string title, string suggestedFileName, string fileTypeName, string extension, Func<string, IReadOnlyList<ProposedFileChangeViewModel>, string> buildContent)
    {
        var path = await _fileSavePickerService.PickSaveFileAsync(title, suggestedFileName, fileTypeName, [extension]);

        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            File.WriteAllText(path, buildContent(Tutorial, Files));
            ExportStatusMessage = string.Format("Tutorial_ExportSucceeded".GetLocalized(), path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ExportStatusMessage = string.Format("Tutorial_ExportFailed".GetLocalized(), ex.Message);
        }
    }

    // Files/IPromptResultStore.Files both hold the same instances (see OnNavigatedTo), and
    // ProposedFileChangeViewModel can't be mutated into a new file's shape in place (Hunks is
    // init-only, ProposedContent has no setter) - so a refined file is swapped in by index in both
    // collections instead, keeping the Diffs page in sync without any new pub/sub wiring.
    private void ReplaceFile(ProposedFileChangeViewModel oldFile, ProposedFileChangeViewModel newFile)
    {
        var filesIndex = Files.IndexOf(oldFile);

        if (filesIndex >= 0)
        {
            Files[filesIndex] = newFile;
        }

        var storeIndex = _promptResultStore.Files.IndexOf(oldFile);

        if (storeIndex >= 0)
        {
            _promptResultStore.Files[storeIndex] = newFile;
        }

        Selected = newFile;
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
