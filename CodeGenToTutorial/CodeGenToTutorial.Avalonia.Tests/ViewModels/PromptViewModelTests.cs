using CodeGenToTutorial.Avalonia.Tests.Fakes;
using CodeGenToTutorial.Avalonia.ViewModels;
using CodeGenToTutorial.Core.Models;

namespace CodeGenToTutorial.Avalonia.Tests.ViewModels;

public class PromptViewModelTests
{
    private readonly FakeClaudeCliService _claudeCliService = new();
    private readonly FakeLocalSettingsService _localSettingsService = new();
    private readonly FakeFolderPickerService _folderPickerService = new();
    private readonly FakePromptResultStore _promptResultStore = new();

    private PromptViewModel CreateSut() =>
        new(_claudeCliService, _localSettingsService, _folderPickerService, _promptResultStore);

    [Fact]
    public async Task RunPromptAsync_SuccessWithNoFiles_StoresTutorialAndReportsCompletion()
    {
        var sut = CreateSut();
        sut.PromptText = "Explain recursion";
        _claudeCliService.ResultToReturn = new ClaudeCliResult { ExitCode = 0, StandardOutput = "Recursion is when a function calls itself." };

        await sut.RunPromptCommand.ExecuteAsync(null);

        Assert.False(sut.IsRunning);
        Assert.Equal("Claude Code finished.", sut.StatusMessage);
        Assert.Equal(1, _promptResultStore.SetResultCallCount);
        Assert.Empty(_promptResultStore.Files);
    }

    [Fact]
    public async Task RunPromptAsync_SuccessWithFiles_ReportsFileCountAndStoresFiles()
    {
        var sut = CreateSut();
        sut.PromptText = "Add a Foo class";
        const string output =
            """
            ## Tutorial
            Adds a Foo class.

            ## Files

            ### File: Foo.cs
            ```
            class Foo {}
            ```
            """;
        _claudeCliService.ResultToReturn = new ClaudeCliResult { ExitCode = 0, StandardOutput = output };

        await sut.RunPromptCommand.ExecuteAsync(null);

        Assert.Equal("Claude Code finished. 1 file(s) ready to review on the Diffs page.", sut.StatusMessage);
        Assert.Single(_promptResultStore.Files);
        Assert.Equal("Foo.cs", _promptResultStore.Files[0].FilePath);
    }

    [Fact]
    public async Task RunPromptAsync_NonZeroExitCode_ReportsFailureAndDoesNotStoreResult()
    {
        var sut = CreateSut();
        sut.PromptText = "Do something";
        _claudeCliService.ResultToReturn = new ClaudeCliResult { ExitCode = 1, StandardError = "boom" };

        await sut.RunPromptCommand.ExecuteAsync(null);

        Assert.False(sut.IsRunning);
        Assert.Equal("Claude Code exited with code 1.", sut.StatusMessage);
        Assert.Equal("boom", sut.CliOutput);
        Assert.Equal(0, _promptResultStore.SetResultCallCount);
    }

    [Fact]
    public async Task RunPromptAsync_CliServiceThrows_ReportsErrorAndResetsIsRunning()
    {
        var sut = CreateSut();
        sut.PromptText = "Do something";
        _claudeCliService.ExceptionToThrow = new InvalidOperationException("claude not found");

        await sut.RunPromptCommand.ExecuteAsync(null);

        Assert.False(sut.IsRunning);
        Assert.Equal("Failed to run Claude Code: claude not found", sut.StatusMessage);
        Assert.Equal(0, _promptResultStore.SetResultCallCount);
    }

    [Fact]
    public async Task RunPromptAsync_PassesInstructionPreambleAndWorkingDirectoryToCliService()
    {
        var sut = CreateSut();
        sut.PromptText = "Add a Foo class";
        sut.WorkingDirectoryPath = "/tmp/my-project";

        await sut.RunPromptCommand.ExecuteAsync(null);

        Assert.Equal("/tmp/my-project", _claudeCliService.LastWorkingDirectory);
        Assert.Contains("Add a Foo class", _claudeCliService.LastPrompt);
        Assert.Contains("The working directory is: /tmp/my-project", _claudeCliService.LastPrompt);
    }

    [Fact]
    public async Task RunPromptAsync_NoWorkingDirectory_DoesNotAppendWorkingDirectoryLine()
    {
        var sut = CreateSut();
        sut.PromptText = "Add a Foo class";

        await sut.RunPromptCommand.ExecuteAsync(null);

        Assert.DoesNotContain("The working directory is:", _claudeCliService.LastPrompt);
    }

    [Fact]
    public void CanRunPrompt_EmptyPromptText_CannotExecute()
    {
        var sut = CreateSut();
        sut.PromptText = string.Empty;

        Assert.False(sut.RunPromptCommand.CanExecute(null));
    }

    [Fact]
    public void CanRunPrompt_NonEmptyPromptText_CanExecute()
    {
        var sut = CreateSut();
        sut.PromptText = "Hello";

        Assert.True(sut.RunPromptCommand.CanExecute(null));
    }

    [Fact]
    public async Task SavePromptAsync_PersistsPromptTextAndSetsStatus()
    {
        var sut = CreateSut();
        sut.PromptText = "Remember me";

        await sut.SavePromptCommand.ExecuteAsync(null);

        Assert.Equal("Prompt saved.", sut.StatusMessage);
        Assert.Equal("Remember me", await _localSettingsService.ReadSettingAsync<string>("SavedPrompt"));
    }

    [Fact]
    public async Task BrowseWorkingDirectoryAsync_FolderPicked_UpdatesAndPersistsPath()
    {
        var sut = CreateSut();
        _folderPickerService.FolderToReturn = "/home/user/project";

        await sut.BrowseWorkingDirectoryCommand.ExecuteAsync(null);

        Assert.Equal("/home/user/project", sut.WorkingDirectoryPath);
        Assert.Equal("/home/user/project", await _localSettingsService.ReadSettingAsync<string>("SavedWorkingDirectory"));
    }

    [Fact]
    public async Task BrowseWorkingDirectoryAsync_NoFolderPicked_LeavesPathUnchanged()
    {
        var sut = CreateSut();
        sut.WorkingDirectoryPath = "/existing/path";
        _folderPickerService.FolderToReturn = null;

        await sut.BrowseWorkingDirectoryCommand.ExecuteAsync(null);

        Assert.Equal("/existing/path", sut.WorkingDirectoryPath);
    }

    [Fact]
    public async Task OnNavigatedTo_RestoresPromptAndWorkingDirectoryFromSettings()
    {
        await _localSettingsService.SaveSettingAsync("SavedPrompt", "Saved prompt text");
        await _localSettingsService.SaveSettingAsync("SavedWorkingDirectory", "/saved/dir");
        var sut = CreateSut();

        sut.OnNavigatedTo(null!);
        await Task.Yield();

        Assert.Equal("Saved prompt text", sut.PromptText);
        Assert.Equal("/saved/dir", sut.WorkingDirectoryPath);
    }
}
