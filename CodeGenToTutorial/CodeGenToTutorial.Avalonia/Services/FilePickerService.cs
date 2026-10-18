using Avalonia.Platform.Storage;

using CodeGenToTutorial.Avalonia.Contracts.Services;

namespace CodeGenToTutorial.Avalonia.Services;

public class FilePickerService : IFilePickerService
{
    public async Task<IReadOnlyList<string>> PickFilesAsync()
    {
        var topLevel = App.MainWindow;
        if (topLevel == null)
        {
            return Array.Empty<string>();
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select context file(s)",
            AllowMultiple = true,
        });

        return files
            .Select(file => file.TryGetLocalPath())
            .Where(path => path != null)
            .Select(path => path!)
            .ToArray();
    }
}
