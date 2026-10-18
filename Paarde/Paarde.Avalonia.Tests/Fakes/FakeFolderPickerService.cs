using Paarde.Avalonia.Contracts.Services;

namespace Paarde.Avalonia.Tests.Fakes;

public class FakeFolderPickerService : IFolderPickerService
{
    public string? FolderToReturn { get; set; }

    public Task<string?> PickFolderAsync() => Task.FromResult(FolderToReturn);
}
