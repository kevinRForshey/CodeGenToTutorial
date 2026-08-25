namespace CodeGenToTutorial.Contracts.Services;

public interface IFolderPickerService
{
    Task<string?> PickFolderAsync();
}
