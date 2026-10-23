using Paarde.Avalonia.Services;
using Paarde.Avalonia.Tests.Fakes;
using Paarde.Avalonia.ViewModels;
using Paarde.Core.Models;

namespace Paarde.Avalonia.Tests.ViewModels;

public class TerminalViewModelTests
{
    private readonly FakeTerminalCommandRunner _terminalCommandRunner = new();
    private readonly FakeLocalSettingsService _localSettingsService = new();

    private TerminalViewModel CreateSut() => new(_terminalCommandRunner, _localSettingsService);

    private async Task SetSavedWorkingDirectoryAsync(string path) =>
        await _localSettingsService.SaveSettingAsync(WorkingDirectorySettingsKeys.WorkingDirectory, path);

    [Fact]
    public async Task OnNavigatedTo_LoadsTheSavedWorkingDirectory()
    {
        await SetSavedWorkingDirectoryAsync("/home/user/project");
        var sut = CreateSut();

        sut.OnNavigatedTo(null!);

        Assert.Equal("/home/user/project", sut.WorkingDirectoryPath);
    }

    [Fact]
    public void RunCommandCommand_BlankCommandText_CannotExecute()
    {
        var sut = CreateSut();
        sut.CommandText = string.Empty;

        Assert.False(sut.RunCommandCommand.CanExecute(null));
    }

    [Fact]
    public void RunCommandCommand_NonBlankCommandText_CanExecute()
    {
        var sut = CreateSut();
        sut.CommandText = "ls";

        Assert.True(sut.RunCommandCommand.CanExecute(null));
    }

    // T16.1.3's onOutputLine marshals each line through Dispatcher.UIThread.Post, same as
    // PromptViewModel.OnTextDelta - not exercised here since nothing pumps Dispatcher.UIThread in
    // this plain xunit host (see PromptViewModelTests' own comment on OnTextDelta/OnToolActivity),
    // so this only verifies the command and working directory are passed through correctly.
    [Fact]
    public async Task RunCommandAsync_RunsTheTypedCommandInTheSavedWorkingDirectory()
    {
        await SetSavedWorkingDirectoryAsync("/home/user/project");
        var sut = CreateSut();
        sut.OnNavigatedTo(null!);
        sut.CommandText = "ls";
        _terminalCommandRunner.LinesToStream = new[] { "one.txt", "two.txt" };
        _terminalCommandRunner.ResultToReturn = new CommandResult(0, "one.txt\ntwo.txt", string.Empty, TimeSpan.FromSeconds(1));

        await sut.RunCommandCommand.ExecuteAsync(null);

        Assert.Equal("ls", _terminalCommandRunner.LastCommand);
        Assert.Equal("/home/user/project", _terminalCommandRunner.LastWorkingDirectoryPath);
    }

    [Fact]
    public async Task RunCommandAsync_SuccessfulCommand_SetsStatusMessageAndStopsRunning()
    {
        await SetSavedWorkingDirectoryAsync("/home/user/project");
        var sut = CreateSut();
        sut.OnNavigatedTo(null!);
        sut.CommandText = "ls";
        _terminalCommandRunner.ResultToReturn = new CommandResult(0, string.Empty, string.Empty, TimeSpan.FromSeconds(1));

        await sut.RunCommandCommand.ExecuteAsync(null);

        Assert.False(sut.IsRunning);
        Assert.NotEmpty(sut.StatusMessage);
    }

    [Fact]
    public async Task RunCommandAsync_FailingCommand_SetsStatusMessageWithExitCode()
    {
        await SetSavedWorkingDirectoryAsync("/home/user/project");
        var sut = CreateSut();
        sut.OnNavigatedTo(null!);
        sut.CommandText = "false";
        _terminalCommandRunner.ResultToReturn = new CommandResult(1, string.Empty, string.Empty, TimeSpan.FromSeconds(1));

        await sut.RunCommandCommand.ExecuteAsync(null);

        Assert.Contains("1", sut.StatusMessage);
    }

    [Fact]
    public async Task RunCommandAsync_Cancelled_SetsStatusMessageAndStopsRunning()
    {
        await SetSavedWorkingDirectoryAsync("/home/user/project");
        var sut = CreateSut();
        sut.OnNavigatedTo(null!);
        sut.CommandText = "sleep 30";
        _terminalCommandRunner.ThrowOperationCanceledOnNextRun = true;

        await sut.RunCommandCommand.ExecuteAsync(null);

        Assert.False(sut.IsRunning);
        Assert.Equal("Cancelled.", sut.StatusMessage);
    }

    [Fact]
    public void CancelCommandCommand_NoRunInProgress_CannotExecute()
    {
        var sut = CreateSut();

        Assert.False(sut.CancelCommandCommand.CanExecute(null));
    }

    [Fact]
    public void ClearOutputCommand_ClearsOutputText()
    {
        var sut = CreateSut();
        sut.OutputText = "some previous output";

        sut.ClearOutputCommand.Execute(null);

        Assert.Equal(string.Empty, sut.OutputText);
    }
}
