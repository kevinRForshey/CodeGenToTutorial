namespace Paarde.Avalonia.Contracts.Services;

public interface IFolderPickerService
{
    Task<string?> PickFolderAsync();
}
