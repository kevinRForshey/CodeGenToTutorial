using Avalonia.Platform.Storage;

using CodeGenToTutorial.Avalonia.Contracts.Services;

namespace CodeGenToTutorial.Avalonia.Services;

public class FolderPickerService : IFolderPickerService
{
    public async Task<string?> PickFolderAsync()
    {
        var topLevel = App.MainWindow;
        if (topLevel == null)
        {
            return null;
        }

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select a project folder",
            AllowMultiple = false,
        });

        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }
}
