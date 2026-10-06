using CodeGenToTutorial.Avalonia.Contracts.Services;

namespace CodeGenToTutorial.Avalonia.Tests.Fakes;

public class FakeFolderPickerService : IFolderPickerService
{
    public string? FolderToReturn { get; set; }

    public Task<string?> PickFolderAsync() => Task.FromResult(FolderToReturn);
}
