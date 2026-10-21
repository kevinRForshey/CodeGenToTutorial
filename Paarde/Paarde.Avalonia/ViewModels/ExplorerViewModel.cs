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

    public ExplorerViewModel(ILocalSettingsService localSettingsService, IPromptResultStore promptResultStore, ILogger<ExplorerViewModel>? logger = null)
    {
        _promptResultStore = promptResultStore;
        _localSettingsService = localSettingsService;
        _logger = logger ?? NullLogger<ExplorerViewModel>.Instance;
    }

    // T8.1.3: open any file in the editor, proposed or not - a read-only preview, deliberately not
    // wired into ProposedFileChangeViewModel.EditedContent for touched files, so Explorer stays a
    // browse/view surface and editing a proposal stays the Tutorial page's job.
    partial void OnSelectedNodeChanged(FileTreeNodeViewModel? value)
    {
        if (value is null || value.IsDirectory)
        {
            SelectedFilePath = string.Empty;
            SelectedFileContent = string.Empty;
            return;
        }

        SelectedFilePath = value.FullPath;
        SelectedFileContent = FileContentReader.ReadOrEmpty(value.FullPath);
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
            StatusMessage = "Explorer_NoWorkingDirectory".GetLocalized();
            return;
        }

        if (!Directory.Exists(WorkingDirectoryPath))
        {
            Root = null;
            StatusMessage = string.Format("Explorer_WorkingDirectoryNotFound".GetLocalized(), WorkingDirectoryPath);
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
            StatusMessage = string.Format("Explorer_LoadFailed".GetLocalized(), ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Failed to read the working directory {WorkingDirectory} while building the Explorer tree.", WorkingDirectoryPath);
            Root = null;
            StatusMessage = string.Format("Explorer_LoadFailed".GetLocalized(), ex.Message);
        }
    }
}
