using Paarde.Avalonia.Tests.Fakes;
using Paarde.Avalonia.ViewModels;
using Paarde.Core.Services;

namespace Paarde.Avalonia.Tests.ViewModels;

public class SettingsViewModelTests
{
    private readonly FakeThemeSelectorService _themeSelectorService = new();
    private readonly FakeLocalSettingsService _localSettingsService = new();
    private readonly FakeSecureStorageService _secureStorageService = new();
    private readonly PromptTemplateProvider _promptTemplateProvider = new();

    private SettingsViewModel CreateSut() => new(_themeSelectorService, _localSettingsService, _secureStorageService, _promptTemplateProvider);

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

    [Fact]
    public void BuildTestCommands_DefaultToDotnetBuildAndDotnetTest()
    {
        var sut = CreateSut();

        Assert.Equal("dotnet build", sut.BuildCommand);
        Assert.Equal("dotnet test", sut.TestCommand);
    }

    [Fact]
    public async Task BuildCommand_WhenChanged_PersistsToSettings()
    {
        var sut = CreateSut();
        sut.BuildCommand = "msbuild";

        Assert.Equal("msbuild", await _localSettingsService.ReadSettingAsync<string>("BuildTestBuildCommand"));
    }

    [Fact]
    public async Task TestCommand_WhenChanged_PersistsToSettings()
    {
        var sut = CreateSut();
        sut.TestCommand = "npm test";

        Assert.Equal("npm test", await _localSettingsService.ReadSettingAsync<string>("BuildTestTestCommand"));
    }

    [Fact]
    public async Task OnNavigatedTo_RestoresSavedBuildTestCommands()
    {
        await _localSettingsService.SaveSettingAsync("BuildTestBuildCommand", "make");
        await _localSettingsService.SaveSettingAsync("BuildTestTestCommand", "make test");
        var sut = CreateSut();

        sut.OnNavigatedTo(null!);
        await Task.Yield();

        Assert.Equal("make", sut.BuildCommand);
        Assert.Equal("make test", sut.TestCommand);
    }

    [Fact]
    public async Task OnNavigatedTo_SavedBuildCommandIsBlank_RestoresTheBlankRatherThanTheDefault()
    {
        await _localSettingsService.SaveSettingAsync("BuildTestBuildCommand", string.Empty);
        var sut = CreateSut();

        sut.OnNavigatedTo(null!);
        await Task.Yield();

        Assert.Equal(string.Empty, sut.BuildCommand);
    }

    [Fact]
    public void DefaultPromptTemplate_IsTheProvidersBuiltInTemplate()
    {
        var sut = CreateSut();

        Assert.Equal(_promptTemplateProvider.GetTutorialInstructionPrompt(), sut.DefaultPromptTemplate);
    }

    [Fact]
    public void PromptTemplateOverride_DefaultsToEmpty()
    {
        var sut = CreateSut();

        Assert.Equal(string.Empty, sut.PromptTemplateOverride);
    }

    [Fact]
    public async Task PromptTemplateOverride_WhenChanged_PersistsToSettings()
    {
        var sut = CreateSut();
        sut.PromptTemplateOverride = "Custom instructions.";

        Assert.Equal("Custom instructions.", await _localSettingsService.ReadSettingAsync<string>("PromptTemplateOverride"));
    }

    [Fact]
    public async Task OnNavigatedTo_RestoresSavedPromptTemplateOverride()
    {
        await _localSettingsService.SaveSettingAsync("PromptTemplateOverride", "Custom instructions.");
        var sut = CreateSut();

        sut.OnNavigatedTo(null!);
        await Task.Yield();

        Assert.Equal("Custom instructions.", sut.PromptTemplateOverride);
    }

    [Fact]
    public void ResetPromptTemplateCommand_CanExecute_OnlyWhenOverrideIsSet()
    {
        var sut = CreateSut();

        Assert.False(sut.ResetPromptTemplateCommand.CanExecute(null));

        sut.PromptTemplateOverride = "Custom instructions.";

        Assert.True(sut.ResetPromptTemplateCommand.CanExecute(null));
    }

    [Fact]
    public void ResetPromptTemplateCommand_Execute_ClearsTheOverride()
    {
        var sut = CreateSut();
        sut.PromptTemplateOverride = "Custom instructions.";

        sut.ResetPromptTemplateCommand.Execute(null);

        Assert.Equal(string.Empty, sut.PromptTemplateOverride);
    }
}
