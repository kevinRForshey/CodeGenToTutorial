using Paarde.Avalonia.Contracts.Services;
using Paarde.Avalonia.Contracts.ViewModels;
using Paarde.Avalonia.Services;
using Paarde.Avalonia.Strings;
using Paarde.Core.Contracts.Services;
using Paarde.Core.Helpers;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Paarde.Avalonia.ViewModels;

// T8.1.1: project explorer - a gitignore-aware file-tree view of the working directory last
// picked on the Prompt page (same persisted setting, via WorkingDirectorySettingsKeys, rather
// than IPromptResultStore.WorkingDirectoryPath - the tree should be browsable as soon as a folder
// is picked, not only after a prompt has actually been run).
public partial class ExplorerViewModel : ObservableObject, INavigationAware
{
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IPromptResultStore _promptResultStore;

    [ObservableProperty]
    private string workingDirectoryPath = string.Empty;

    [ObservableProperty]
    private FileTreeNodeViewModel? root;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    public ExplorerViewModel(ILocalSettingsService localSettingsService, IPromptResultStore promptResultStore)
    {
        _promptResultStore = promptResultStore;
        _localSettingsService = localSettingsService;
    }

    public async void OnNavigatedTo(object parameter)
    {
        WorkingDirectoryPath = await _localSettingsService.ReadSettingAsync<string>(WorkingDirectorySettingsKeys.WorkingDirectory) ?? string.Empty;
        RefreshTree();
    }

    public void OnNavigatedFrom()
    {
    }

    [RelayCommand]
    private void RefreshTree()
    {
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
            Root = null;
            StatusMessage = string.Format("Explorer_LoadFailed".GetLocalized(), ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            Root = null;
            StatusMessage = string.Format("Explorer_LoadFailed".GetLocalized(), ex.Message);
        }
    }
}
