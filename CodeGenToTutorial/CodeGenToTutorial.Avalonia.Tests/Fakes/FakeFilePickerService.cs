using CodeGenToTutorial.Avalonia.Contracts.Services;

namespace CodeGenToTutorial.Avalonia.Tests.Fakes;

public class FakeFilePickerService : IFilePickerService
{
    public IReadOnlyList<string> FilesToReturn { get; set; } = Array.Empty<string>();

    public Task<IReadOnlyList<string>> PickFilesAsync() => Task.FromResult(FilesToReturn);
}
