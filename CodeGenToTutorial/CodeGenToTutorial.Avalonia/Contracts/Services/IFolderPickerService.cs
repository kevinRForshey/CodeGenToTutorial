namespace CodeGenToTutorial.Avalonia.Contracts.Services;

public interface IFolderPickerService
{
    Task<string?> PickFolderAsync();
}
