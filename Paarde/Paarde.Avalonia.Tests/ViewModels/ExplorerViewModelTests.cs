using Paarde.Avalonia.Services;
using Paarde.Avalonia.Tests.Fakes;
using Paarde.Avalonia.ViewModels;
using Paarde.Core.ViewModels;

namespace Paarde.Avalonia.Tests.ViewModels;

public class ExplorerViewModelTests : IDisposable
{
    private readonly FakeLocalSettingsService _localSettingsService = new();
    private readonly FakePromptResultStore _promptResultStore = new();
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

    private ExplorerViewModel CreateSut() => new(_localSettingsService, _promptResultStore);

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

    [Fact]
    public async Task OnNavigatedTo_FileIsPartOfTheCurrentProposal_IsMarkedTouched()
    {
        File.WriteAllText(Path.Combine(_workingDirectory, "a.txt"), "content");
        File.WriteAllText(Path.Combine(_workingDirectory, "b.txt"), "content");
        _promptResultStore.SetResult("tutorial", [new ProposedFileChangeViewModel("a.txt", "proposed", "original")], _workingDirectory);
        await SetSavedWorkingDirectoryAsync(_workingDirectory);
        var sut = CreateSut();

        sut.OnNavigatedTo(null!);

        var touched = sut.Root!.Children.Single(node => node.Name == "a.txt");
        var untouched = sut.Root!.Children.Single(node => node.Name == "b.txt");
        Assert.True(touched.IsTouched);
        Assert.False(untouched.IsTouched);
    }

    [Fact]
    public async Task OnNavigatedTo_NoCurrentProposal_NothingIsMarkedTouched()
    {
        File.WriteAllText(Path.Combine(_workingDirectory, "a.txt"), "content");
        await SetSavedWorkingDirectoryAsync(_workingDirectory);
        var sut = CreateSut();

        sut.OnNavigatedTo(null!);

        Assert.False(Assert.Single(sut.Root!.Children).IsTouched);
    }

    [Fact]
    public async Task OnNavigatedTo_TouchedFileIsInASubdirectory_IsMarkedTouched()
    {
        Directory.CreateDirectory(Path.Combine(_workingDirectory, "src"));
        File.WriteAllText(Path.Combine(_workingDirectory, "src", "a.txt"), "content");
        _promptResultStore.SetResult("tutorial", [new ProposedFileChangeViewModel(Path.Combine("src", "a.txt"), "proposed", "original")], _workingDirectory);
        await SetSavedWorkingDirectoryAsync(_workingDirectory);
        var sut = CreateSut();

        sut.OnNavigatedTo(null!);

        var srcFolder = Assert.Single(sut.Root!.Children);
        Assert.True(Assert.Single(srcFolder.Children).IsTouched);
    }

    [Fact]
    public async Task SelectedNode_SetToAFile_LoadsItsContentForPreview()
    {
        File.WriteAllText(Path.Combine(_workingDirectory, "a.txt"), "file content");
        await SetSavedWorkingDirectoryAsync(_workingDirectory);
        var sut = CreateSut();
        sut.OnNavigatedTo(null!);
        var fileNode = Assert.Single(sut.Root!.Children);

        sut.SelectedNode = fileNode;

        Assert.Equal(fileNode.FullPath, sut.SelectedFilePath);
        Assert.Equal("file content", sut.SelectedFileContent);
    }

    [Fact]
    public async Task SelectedNode_SetToADirectory_ClearsThePreview()
    {
        Directory.CreateDirectory(Path.Combine(_workingDirectory, "src"));
        File.WriteAllText(Path.Combine(_workingDirectory, "src", "a.txt"), "content");
        await SetSavedWorkingDirectoryAsync(_workingDirectory);
        var sut = CreateSut();
        sut.OnNavigatedTo(null!);
        var folderNode = Assert.Single(sut.Root!.Children);

        sut.SelectedNode = folderNode;

        Assert.Empty(sut.SelectedFilePath);
        Assert.Empty(sut.SelectedFileContent);
    }

    [Fact]
    public async Task SelectedNode_SetToNull_ClearsThePreview()
    {
        File.WriteAllText(Path.Combine(_workingDirectory, "a.txt"), "content");
        await SetSavedWorkingDirectoryAsync(_workingDirectory);
        var sut = CreateSut();
        sut.OnNavigatedTo(null!);
        sut.SelectedNode = sut.Root!.Children.Single();

        sut.SelectedNode = null;

        Assert.Empty(sut.SelectedFilePath);
        Assert.Empty(sut.SelectedFileContent);
    }

    [Fact]
    public async Task RefreshTreeCommand_AFileWasSelected_ClearsTheStaleSelectionAndPreview()
    {
        File.WriteAllText(Path.Combine(_workingDirectory, "a.txt"), "content");
        await SetSavedWorkingDirectoryAsync(_workingDirectory);
        var sut = CreateSut();
        sut.OnNavigatedTo(null!);
        sut.SelectedNode = sut.Root!.Children.Single();

        sut.RefreshTreeCommand.Execute(null);

        Assert.Null(sut.SelectedNode);
        Assert.Empty(sut.SelectedFilePath);
        Assert.Empty(sut.SelectedFileContent);
    }
}
