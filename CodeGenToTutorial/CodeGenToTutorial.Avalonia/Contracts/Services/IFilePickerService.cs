namespace CodeGenToTutorial.Avalonia.Contracts.Services;

public interface IFilePickerService
{
    Task<IReadOnlyList<string>> PickFilesAsync();
}
