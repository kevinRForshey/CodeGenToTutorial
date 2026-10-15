using CodeGenToTutorial.Avalonia.Tests.Fakes;
using CodeGenToTutorial.Avalonia.ViewModels;

namespace CodeGenToTutorial.Avalonia.Tests.ViewModels;

public class SettingsViewModelTests
{
    private readonly FakeThemeSelectorService _themeSelectorService = new();
    private readonly FakeLocalSettingsService _localSettingsService = new();

    private SettingsViewModel CreateSut() => new(_themeSelectorService, _localSettingsService);

    [Fact]
    public void ClaudeCliSettings_DefaultToClaudeAndDashP()
    {
        var sut = CreateSut();

        Assert.Equal("claude", sut.ClaudeCliExecutablePath);
        Assert.Equal("-p", sut.ClaudeCliArguments);
    }

    [Fact]
    public async Task ClaudeCliExecutablePath_WhenChanged_PersistsToSettings()
    {
        var sut = CreateSut();
        sut.ClaudeCliExecutablePath = "/usr/local/bin/claude";

        Assert.Equal("/usr/local/bin/claude", await _localSettingsService.ReadSettingAsync<string>("ClaudeCliExecutablePath"));
    }

    [Fact]
    public async Task ClaudeCliArguments_WhenChanged_PersistsToSettings()
    {
        var sut = CreateSut();
        sut.ClaudeCliArguments = "-p --model=opus";

        Assert.Equal("-p --model=opus", await _localSettingsService.ReadSettingAsync<string>("ClaudeCliArguments"));
    }

    [Fact]
    public async Task OnNavigatedTo_RestoresSavedClaudeCliSettings()
    {
        await _localSettingsService.SaveSettingAsync("ClaudeCliExecutablePath", "/opt/claude/claude");
        await _localSettingsService.SaveSettingAsync("ClaudeCliArguments", "-p --verbose");
        var sut = CreateSut();

        sut.OnNavigatedTo(null!);
        await Task.Yield();

        Assert.Equal("/opt/claude/claude", sut.ClaudeCliExecutablePath);
        Assert.Equal("-p --verbose", sut.ClaudeCliArguments);
    }

    [Fact]
    public async Task OnNavigatedTo_NoSavedClaudeCliSettings_LeavesDefaults()
    {
        var sut = CreateSut();

        sut.OnNavigatedTo(null!);
        await Task.Yield();

        Assert.Equal("claude", sut.ClaudeCliExecutablePath);
        Assert.Equal("-p", sut.ClaudeCliArguments);
    }
}
