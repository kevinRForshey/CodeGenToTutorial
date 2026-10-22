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
public partial class ExplorerViewModel : ObservableObject, INavigationAware
{
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IPromptResultStore _promptResultStore;
    private readonly ILlmRunner _llmRunner;
    private readonly ILogger<ExplorerViewModel> _logger;

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

    public ExplorerViewModel(
        ILocalSettingsService localSettingsService,
        IPromptResultStore promptResultStore,
        ILlmRunner llmRunner,
        ILogger<ExplorerViewModel>? logger = null)
    {
        _promptResultStore = promptResultStore;
        _localSettingsService = localSettingsService;
        _llmRunner = llmRunner;
        _logger = logger ?? NullLogger<ExplorerViewModel>.Instance;
    }

    // T8.1.3: open any file in the editor, proposed or not - a read-only preview, deliberately not
    // wired into ProposedFileChangeViewModel.EditedContent for touched files, so Explorer stays a
    // browse/view surface and editing a proposal stays the Tutorial page's job.
    partial void OnSelectedNodeChanged(FileTreeNodeViewModel? value)
    {
        // T10.1.3: a previous selection's explanation would otherwise linger and read as if it
        // described whatever's selected now.
        FileOverview = string.Empty;

        if (value is null || value.IsDirectory)
        {
            SelectedFilePath = string.Empty;
            SelectedFileContent = string.Empty;
            return;
        }

        SelectedFilePath = value.FullPath;
        SelectedFileContent = FileContentReader.ReadOrEmpty(value.FullPath);
    }

    partial void OnSelectedFilePathChanged(string value) => ExplainSelectedFileCommand.NotifyCanExecuteChanged();

    partial void OnIsLoadingOverviewChanged(bool value) => ExplainSelectedFileCommand.NotifyCanExecuteChanged();

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
