namespace Paarde.Avalonia.Contracts.Services;

public interface IFilePickerService
{
    Task<IReadOnlyList<string>> PickFilesAsync();
}
