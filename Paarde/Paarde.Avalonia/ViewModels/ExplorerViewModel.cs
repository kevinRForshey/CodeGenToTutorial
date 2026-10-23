using Paarde.Avalonia.Contracts.Services;
using Paarde.Avalonia.Contracts.ViewModels;
using Paarde.Avalonia.Services;
using Paarde.Avalonia.Strings;
using Paarde.Core.Contracts.Services;
using Paarde.Core.Helpers;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Paarde.Avalonia.ViewModels;

// T8.1.1: project explorer - a gitignore-aware file-tree view of the working directory last
// picked on the Prompt page (same persisted setting, via WorkingDirectorySettingsKeys, rather
// than IPromptResultStore.WorkingDirectoryPath - the tree should be browsable as soon as a folder
// is picked, not only after a prompt has actually been run).
public partial class ExplorerViewModel : ObservableObject, INavigationAware, IConfirmNavigationAway
{
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IPromptResultStore _promptResultStore;
    private readonly ILlmRunner _llmRunner;
    private readonly IFileChangeApplyService _fileChangeApplyService;
    private readonly IDialogService _dialogService;
    private readonly ILogger<ExplorerViewModel> _logger;

    // T10.2.1: what's actually on disk as of the last successful load/save, compared against
    // SelectedFileContent to derive IsFileDirty - same IsDirty-via-divergence shape
    // ProposedFileChangeViewModel already uses, just without a third (proposed) baseline since
    // there's no LLM proposal in this flow.
    private string _originalFileContentOnDisk = string.Empty;

    // T10.2.5: captured by OnSelectedNodeChanging (fires before SelectedNode's backing field is
    // updated) so a declined "discard unsaved edits?" prompt can put the tree's selection back
    // where it was.
    private FileTreeNodeViewModel? _nodeBeforeSelectionChange;

    [ObservableProperty]
    private string workingDirectoryPath = string.Empty;

    [ObservableProperty]
    private FileTreeNodeViewModel? root;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private FileTreeNodeViewModel? selectedNode;

    [ObservableProperty]
    private string selectedFilePath = string.Empty;

    [ObservableProperty]
    private string selectedFileContent = string.Empty;

    // T10.1.1: the AI-generated tutorial-style explanation of SelectedFilePath, requested on demand
    // via ExplainSelectedFileCommand rather than automatically on every selection - running a prompt
    // on every tree click would be surprising cost/latency, same reasoning
    // TutorialViewModel.RefineSelectedFileCommand/TutorialStepViewModel.ExplainFurtherCommand are
    // both explicit actions rather than eager.
    [ObservableProperty]
    private string fileOverview = string.Empty;

    [ObservableProperty]
    private bool isLoadingOverview;

    // Epic 15, T15.2: the most recent ghost-text completion CodeEditor asked for - set to null then
    // immediately to the new value (rather than straight to the new value) so a repeat suggestion
    // (same text twice in a row) still raises PropertyChanged; Avalonia's property system otherwise
    // short-circuits a set that equals the current value, and CodeEditor would never redisplay it.
    [ObservableProperty]
    private string? ghostTextSuggestion;

    private CancellationTokenSource? _ghostTextCancellationTokenSource;

    // T10.2.1: whether SelectedFileContent has diverged from _originalFileContentOnDisk.
    [ObservableProperty]
    private bool isFileDirty;

    public ExplorerViewModel(
        ILocalSettingsService localSettingsService,
        IPromptResultStore promptResultStore,
        ILlmRunner llmRunner,
        IFileChangeApplyService fileChangeApplyService,
        IDialogService dialogService,
        ILogger<ExplorerViewModel>? logger = null)
    {
        _promptResultStore = promptResultStore;
        _localSettingsService = localSettingsService;
        _llmRunner = llmRunner;
        _fileChangeApplyService = fileChangeApplyService;
        _dialogService = dialogService;
        _logger = logger ?? NullLogger<ExplorerViewModel>.Instance;
    }

    // T10.2.5: IConfirmNavigationAway - checked by NavigationService.OnNavigating before leaving
    // this page, same mechanism TutorialViewModel already uses for its own unsaved edits.
    public bool HasUnsavedChanges => IsFileDirty;

    partial void OnSelectedNodeChanging(FileTreeNodeViewModel? oldValue, FileTreeNodeViewModel? newValue) =>
        _nodeBeforeSelectionChange = oldValue;

    // T8.1.3: open any file in the editor, proposed or not - a read-only preview, deliberately not
    // wired into ProposedFileChangeViewModel.EditedContent for touched files, so Explorer stays a
    // browse/view surface and editing a proposal stays the Tutorial page's job.
    //
    // T10.2.5: switching the tree selection is not a page navigation, so IConfirmNavigationAway's
    // guard above never sees it - this needs its own unsaved-edits check. Re-entrancy guard first:
    // reselecting the node that's already loaded (including the revert this method itself performs
    // below when the user declines to discard) must not re-read from disk and wipe out the very
    // edits being protected.
    partial void OnSelectedNodeChanged(FileTreeNodeViewModel? value)
    {
        var newPath = value is { IsDirectory: false } ? value.FullPath : string.Empty;

        if (newPath == SelectedFilePath)
        {
            return;
        }

        if (IsFileDirty)
        {
            ConfirmDiscardDirtyFileThenLoad(value);
            return;
        }

        LoadNode(value);
    }

    private async void ConfirmDiscardDirtyFileThenLoad(FileTreeNodeViewModel? value)
    {
        try
        {
            var discard = await _dialogService.ConfirmAsync(
                "Explorer_DiscardEditsConfirmTitle".Resolve(),
                "Explorer_DiscardEditsConfirmMessage".Resolve(),
                "Explorer_DiscardEditsConfirmButton".Resolve(),
                "Explorer_DiscardEditsCancelButton".Resolve());

            if (discard)
            {
                LoadNode(value);
            }
            else
            {
                SelectedNode = _nodeBeforeSelectionChange;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to confirm discarding unsaved Explorer edits.");
        }
    }

    private void LoadNode(FileTreeNodeViewModel? value)
    {
        // T10.1.3: a previous selection's explanation would otherwise linger and read as if it
        // described whatever's selected now.
        FileOverview = string.Empty;

        if (value is null || value.IsDirectory)
        {
            SelectedFilePath = string.Empty;
            _originalFileContentOnDisk = string.Empty;
            SelectedFileContent = string.Empty;
            return;
        }

        SelectedFilePath = value.FullPath;
        _originalFileContentOnDisk = FileContentReader.ReadOrEmpty(value.FullPath);
        SelectedFileContent = _originalFileContentOnDisk;
    }

    partial void OnSelectedFilePathChanged(string value) => ExplainSelectedFileCommand.NotifyCanExecuteChanged();

    partial void OnSelectedFileContentChanged(string value) => IsFileDirty = value != _originalFileContentOnDisk;

    partial void OnIsLoadingOverviewChanged(bool value) => ExplainSelectedFileCommand.NotifyCanExecuteChanged();

    partial void OnIsFileDirtyChanged(bool value)
    {
        SaveSelectedFileCommand.NotifyCanExecuteChanged();
        RevertSelectedFileToDiskCommand.NotifyCanExecuteChanged();
    }

    // T10.2.2: writes through IFileChangeApplyService.WriteRaw rather than a raw File.WriteAllText,
    // so a manual save gets the same path-containment check (FilePathContainment.Resolve, T4.1.1)
    // and encoding/line-ending preservation (FileEncodingDetector, T4.1.3) every other write in this
    // app already gets.
    [RelayCommand(CanExecute = nameof(CanSaveSelectedFile))]
    private void SaveSelectedFile()
    {
        var relativePath = Path.GetRelativePath(WorkingDirectoryPath, SelectedFilePath).Replace(Path.DirectorySeparatorChar, '/');

        // T10.2.3: refuses outright rather than offering an "apply anyway" override - unlike the
        // batch apply flow (which already has a confirm-dialog service wired for exactly that),
        // this is a single ad-hoc edit and a narrower write path that doesn't need to reuse
        // allowProtectedPaths' override semantics on day one.
        if (ProtectedPathChecker.IsProtected(WorkingDirectoryPath, relativePath))
        {
            StatusMessage = string.Format("Explorer_SaveBlockedProtectedPath".Resolve(), relativePath);
            return;
        }

        // T10.2.4: the same check ApplyOrchestrationService.ApplyOne does before an LLM apply
        // (currentDiskContent != file.OriginalContent, T3.3.3) - if the file changed on disk since
        // it was opened here, refuse rather than silently clobbering whatever wrote it.
        var currentDiskContent = FileContentReader.ReadOrEmpty(SelectedFilePath);

        if (currentDiskContent != _originalFileContentOnDisk)
        {
            StatusMessage = string.Format("Explorer_SaveConflict".Resolve(), relativePath);
            return;
        }

        try
        {
            _fileChangeApplyService.WriteRaw(relativePath, SelectedFileContent, WorkingDirectoryPath);
            _originalFileContentOnDisk = SelectedFileContent;
            IsFileDirty = false;
            StatusMessage = string.Format("Explorer_SaveSucceeded".Resolve(), relativePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Failed to save {FilePath} in {WorkingDirectory}.", relativePath, WorkingDirectoryPath);
            StatusMessage = string.Format("Explorer_SaveFailed".Resolve(), relativePath, ex.Message);
        }
    }

    private bool CanSaveSelectedFile() => IsFileDirty;

    // T10.2.6: Undo/Redo themselves are free once CodeEditor stops being read-only (AvaloniaEdit's
    // TextAreaDefaultInputHandler already wires Ctrl+Z/Ctrl+Y) - this covers the case undo history
    // doesn't: discarding every edit at once and going back to what's on disk right now, which may
    // have changed since this file was opened.
    [RelayCommand(CanExecute = nameof(CanRevertSelectedFileToDisk))]
    private void RevertSelectedFileToDisk()
    {
        _originalFileContentOnDisk = FileContentReader.ReadOrEmpty(SelectedFilePath);
        SelectedFileContent = _originalFileContentOnDisk;
    }

    private bool CanRevertSelectedFileToDisk() => IsFileDirty;

    // T10.1.1/T10.1.2: reuses the plain-prose prompt pattern TutorialStepViewModel.ExplainFurtherAsync
    // already established (no JSON, no markdown fences - this is prose for a human, not something
    // ClaudeJsonResponseParser needs to parse). No resumeSessionId - this is a one-off lookup against
    // whatever file is open in Explorer, not a continuation of the active tutorial's conversation.
    [RelayCommand(CanExecute = nameof(CanExplainSelectedFile))]
    private async Task ExplainSelectedFileAsync()
    {
        IsLoadingOverview = true;
        FileOverview = string.Empty;

        try
        {
            var prompt =
                "Explain this file in a tutorial style - what it does, how it's structured, and " +
                "anything a newcomer to this codebase should notice. Respond in plain prose only - " +
                "no JSON, no markdown fences.\n\n" +
                $"File: {SelectedFilePath}\n\n{SelectedFileContent}";

            var result = await _llmRunner.RunPromptAsync(prompt, WorkingDirectoryPath);

            FileOverview = result.Success
                ? result.Output.Trim()
                : string.Format("Explorer_ExplainFailed".Resolve(), result.ErrorMessage);
        }
        finally
        {
            IsLoadingOverview = false;
        }
    }

    private bool CanExplainSelectedFile() => !IsLoadingOverview && !string.IsNullOrEmpty(SelectedFilePath);

    // Epic 15, T15.2.1/T15.2.2: handles CodeEditor.GhostTextRequested. Deliberately reuses whichever
    // ILlmRunner is already configured (same as ExplainSelectedFileAsync above) rather than adding a
    // dedicated fast-model setting for completions - latency will vary with the active runner (a
    // ClaudeCli process spawn is a lot slower than a direct API call), which is a real, currently
    // unverified-in-this-sandbox UX risk the roadmap flagged as an open question (F15.2.2). The
    // short per-request timeout plus CodeEditor's own staleness check (dropping a suggestion whose
    // caret offset no longer matches) are the mitigation for now; a dedicated faster model is the
    // follow-up if this proves too slow in practice, not something to build ahead of that evidence.
    //
    // No resumeSessionId - a one-off lookup against whatever's open in Explorer, not a continuation
    // of any other conversation, same reasoning ExplainSelectedFileAsync's comment gives.
    public async Task RequestGhostTextCompletionAsync(string textBeforeCaret, string textAfterCaret)
    {
        _ghostTextCancellationTokenSource?.Cancel();
        var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        _ghostTextCancellationTokenSource = cancellationTokenSource;

        try
        {
            var prompt =
                "Complete the code at the <cursor/> marker below. Respond with ONLY the text that " +
                "should be inserted at the cursor - no explanation, no markdown fences, nothing before " +
                "or after it. If nothing reasonable completes the code there, respond with nothing.\n\n" +
                $"File: {SelectedFilePath}\n\n{textBeforeCaret}<cursor/>{textAfterCaret}";

            var result = await _llmRunner.RunPromptAsync(prompt, WorkingDirectoryPath, cancellationToken: cancellationTokenSource.Token);
            if (cancellationTokenSource.IsCancellationRequested || !result.Success || string.IsNullOrWhiteSpace(result.Output))
            {
                return;
            }

            GhostTextSuggestion = null;
            GhostTextSuggestion = result.Output.Trim();
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer request, or timed out - either way, there's nothing to show.
        }
    }

    // T9.2.3: async void because INavigationAware.OnNavigatedTo returns void - wrapped so a
    // corrupt settings file (the one thing ReadSettingAsync can actually throw on) is logged
    // rather than unwound unobserved out of NavigationService.OnNavigated; RefreshTree's own
    // try/catch below already covers everything that can go wrong once WorkingDirectoryPath is in
    // hand.
    public async void OnNavigatedTo(object parameter)
    {
        try
        {
            WorkingDirectoryPath = await _localSettingsService.ReadSettingAsync<string>(WorkingDirectorySettingsKeys.WorkingDirectory) ?? string.Empty;
            RefreshTree();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load the saved working directory while navigating to the Explorer page.");
        }
    }

    public void OnNavigatedFrom()
    {
    }

    [RelayCommand]
    private void RefreshTree()
    {
        // Rebuilding Root below creates an entirely new node tree, so any previously selected node
        // would otherwise be left pointing at a stale, now-orphaned instance.
        SelectedNode = null;

        if (string.IsNullOrWhiteSpace(WorkingDirectoryPath))
        {
            Root = null;
            StatusMessage = "Explorer_NoWorkingDirectory".Resolve();
            return;
        }

        if (!Directory.Exists(WorkingDirectoryPath))
        {
            Root = null;
            StatusMessage = string.Format("Explorer_WorkingDirectoryNotFound".Resolve(), WorkingDirectoryPath);
            return;
        }

        try
        {
            var touchedFullPaths = _promptResultStore.Files
                .Select(file => Path.GetFullPath(Path.Combine(WorkingDirectoryPath, file.FilePath)))
                .ToHashSet(StringComparer.Ordinal);

            Root = FileTreeNodeViewModel.FromEntry(FileTreeBuilder.Build(WorkingDirectoryPath), touchedFullPaths);
            StatusMessage = string.Empty;
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Failed to read the working directory {WorkingDirectory} while building the Explorer tree.", WorkingDirectoryPath);
            Root = null;
            StatusMessage = string.Format("Explorer_LoadFailed".Resolve(), ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Failed to read the working directory {WorkingDirectory} while building the Explorer tree.", WorkingDirectoryPath);
            Root = null;
            StatusMessage = string.Format("Explorer_LoadFailed".Resolve(), ex.Message);
        }
    }
}
