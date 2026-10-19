using Paarde.Avalonia.Services;
using Paarde.Avalonia.Tests.Fakes;
using Paarde.Avalonia.ViewModels;

namespace Paarde.Avalonia.Tests.ViewModels;

public class ExplorerViewModelTests : IDisposable
{
    private readonly FakeLocalSettingsService _localSettingsService = new();
    private readonly string _workingDirectory;

    public ExplorerViewModelTests()
    {
        _workingDirectory = Directory.CreateTempSubdirectory("PaardeTests_").FullName;
    }

    public void Dispose()
    {
        if (Directory.Exists(_workingDirectory))
        {
            Directory.Delete(_workingDirectory, recursive: true);
        }
    }

    private ExplorerViewModel CreateSut() => new(_localSettingsService);

    private async Task SetSavedWorkingDirectoryAsync(string path) =>
        await _localSettingsService.SaveSettingAsync(WorkingDirectorySettingsKeys.WorkingDirectory, path);

    [Fact]
    public void OnNavigatedTo_NoSavedWorkingDirectory_HasNoRootAndExplainsWhy()
    {
        var sut = CreateSut();

        sut.OnNavigatedTo(null!);

        Assert.Null(sut.Root);
        Assert.NotEmpty(sut.StatusMessage);
    }

    [Fact]
    public async Task OnNavigatedTo_SavedWorkingDirectoryThatNoLongerExists_HasNoRootAndExplainsWhy()
    {
        await SetSavedWorkingDirectoryAsync(Path.Combine(_workingDirectory, "does-not-exist"));
        var sut = CreateSut();

        sut.OnNavigatedTo(null!);

        Assert.Null(sut.Root);
        Assert.NotEmpty(sut.StatusMessage);
    }

    [Fact]
    public async Task OnNavigatedTo_SavedWorkingDirectory_BuildsTheFileTreeAndClearsAnyStatusMessage()
    {
        File.WriteAllText(Path.Combine(_workingDirectory, "a.txt"), "content");
        await SetSavedWorkingDirectoryAsync(_workingDirectory);
        var sut = CreateSut();

        sut.OnNavigatedTo(null!);

        Assert.NotNull(sut.Root);
        Assert.Equal("a.txt", Assert.Single(sut.Root!.Children).Name);
        Assert.Empty(sut.StatusMessage);
    }

    [Fact]
    public async Task RefreshTreeCommand_AfterAFileIsAdded_PicksUpTheNewFile()
    {
        await SetSavedWorkingDirectoryAsync(_workingDirectory);
        var sut = CreateSut();
        sut.OnNavigatedTo(null!);
        Assert.Empty(sut.Root!.Children);

        File.WriteAllText(Path.Combine(_workingDirectory, "new.txt"), "content");
        sut.RefreshTreeCommand.Execute(null);

        Assert.Equal("new.txt", Assert.Single(sut.Root!.Children).Name);
    }
}
