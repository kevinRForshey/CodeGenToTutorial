using Paarde.Avalonia.Models;
using Paarde.Avalonia.Services;
using Paarde.Avalonia.Tests.Fakes;
using Paarde.Avalonia.ViewModels;
using Paarde.Core.Models;

namespace Paarde.Avalonia.Tests.ViewModels;

public class MainViewModelTests
{
    private readonly FakeRunSessionStore _runSessionStore = new();
    private readonly FakeLocalSettingsService _localSettingsService = new();
    private readonly FakeNavigationService _navigationService = new();

    private MainViewModel CreateSut() => new(_runSessionStore, _localSettingsService, _navigationService);

    private static RunSession CreateSession(string userPromptText, string workingDirectoryPath, DateTimeOffset startedAt) => new(
        Guid.NewGuid().ToString("N"),
        startedAt,
        workingDirectoryPath,
        "composed prompt",
        "raw output",
        "tutorial text",
        Array.Empty<RunSessionFile>(),
        Array.Empty<ApplyHistoryEntry>(),
        userPromptText);

    [Fact]
    public void OnNavigatedTo_NoSessions_HasNoRecentRunsOrProjects()
    {
        var sut = CreateSut();

        sut.OnNavigatedTo(null!);

        Assert.False(sut.HasRecentRuns);
        Assert.False(sut.HasRecentProjects);
        Assert.Empty(sut.RecentRuns);
        Assert.Empty(sut.RecentProjects);
    }

    [Fact]
    public void OnNavigatedTo_PopulatesRecentRunsFromListSessions()
    {
        _runSessionStore.SessionsToList = new[]
        {
            CreateSession("second prompt", "/working/dir", DateTimeOffset.UtcNow),
            CreateSession("first prompt", "/working/dir", DateTimeOffset.UtcNow.AddMinutes(-1)),
        };
        var sut = CreateSut();

        sut.OnNavigatedTo(null!);

        Assert.Equal(2, sut.RecentRuns.Count);
        Assert.True(sut.HasRecentRuns);
    }

    [Fact]
    public void OnNavigatedTo_CapsRecentRunsAtFive()
    {
        _runSessionStore.SessionsToList = Enumerable.Range(0, 8)
            .Select(i => CreateSession($"prompt {i}", "/working/dir", DateTimeOffset.UtcNow.AddMinutes(-i)))
            .ToList();
        var sut = CreateSut();

        sut.OnNavigatedTo(null!);

        Assert.Equal(5, sut.RecentRuns.Count);
    }

    [Fact]
    public void OnNavigatedTo_RecentProjectsAreDistinctWorkingDirectories()
    {
        _runSessionStore.SessionsToList = new[]
        {
            CreateSession("third", "/working/dir-a", DateTimeOffset.UtcNow),
            CreateSession("second", "/working/dir-b", DateTimeOffset.UtcNow.AddMinutes(-1)),
            CreateSession("first", "/working/dir-a", DateTimeOffset.UtcNow.AddMinutes(-2)),
        };
        var sut = CreateSut();

        sut.OnNavigatedTo(null!);

        Assert.Equal(new[] { "/working/dir-a", "/working/dir-b" }, sut.RecentProjects);
        Assert.True(sut.HasRecentProjects);
    }

    [Fact]
    public void NoSelection_CommandsCannotExecute()
    {
        var sut = CreateSut();

        sut.OnNavigatedTo(null!);

        Assert.False(sut.OpenRecentRunCommand.CanExecute(null));
        Assert.False(sut.OpenRecentProjectCommand.CanExecute(null));
    }

    [Fact]
    public void OpenRecentRunCommand_NavigatesToPromptPageWithAutoRunFalse()
    {
        _runSessionStore.SessionsToList = new[] { CreateSession("fork this prompt", "/working/dir", DateTimeOffset.UtcNow) };
        var sut = CreateSut();
        sut.OnNavigatedTo(null!);
        sut.SelectedRecentRun = sut.RecentRuns[0];

        Assert.True(sut.OpenRecentRunCommand.CanExecute(null));
        sut.OpenRecentRunCommand.Execute(null);

        Assert.Equal(1, _navigationService.NavigateToCallCount);
        Assert.Equal(typeof(PromptViewModel).FullName, _navigationService.LastPageKey);
        var request = Assert.IsType<PromptLaunchRequest>(_navigationService.LastParameter);
        Assert.Equal("fork this prompt", request.PromptText);
        Assert.Equal("/working/dir", request.WorkingDirectoryPath);
        Assert.False(request.AutoRun);
    }

    [Fact]
    public async Task OpenRecentProjectCommand_SavesWorkingDirectoryAndNavigatesWithoutParameter()
    {
        _runSessionStore.SessionsToList = new[] { CreateSession("prompt", "/working/dir", DateTimeOffset.UtcNow) };
        var sut = CreateSut();
        sut.OnNavigatedTo(null!);
        sut.SelectedRecentProject = sut.RecentProjects[0];

        Assert.True(sut.OpenRecentProjectCommand.CanExecute(null));
        await sut.OpenRecentProjectCommand.ExecuteAsync(null);

        Assert.Equal("/working/dir", await _localSettingsService.ReadSettingAsync<string>(WorkingDirectorySettingsKeys.WorkingDirectory));
        Assert.Equal(1, _navigationService.NavigateToCallCount);
        Assert.Equal(typeof(PromptViewModel).FullName, _navigationService.LastPageKey);
        Assert.Null(_navigationService.LastParameter);
    }

    [Fact]
    public void SendQuickPromptCommand_CanExecute_OnlyWhenTextIsNotBlank()
    {
        var sut = CreateSut();

        Assert.False(sut.SendQuickPromptCommand.CanExecute(null));

        sut.QuickPromptText = "   ";
        Assert.False(sut.SendQuickPromptCommand.CanExecute(null));

        sut.QuickPromptText = "build a thing";
        Assert.True(sut.SendQuickPromptCommand.CanExecute(null));
    }

    [Fact]
    public async Task SendQuickPromptCommand_NavigatesWithTypedTextAndLastSavedWorkingDirectory()
    {
        await _localSettingsService.SaveSettingAsync(WorkingDirectorySettingsKeys.WorkingDirectory, "/saved/dir");
        var sut = CreateSut();
        sut.QuickPromptText = "add a feature";

        await sut.SendQuickPromptCommand.ExecuteAsync(null);

        Assert.Equal(1, _navigationService.NavigateToCallCount);
        Assert.Equal(typeof(PromptViewModel).FullName, _navigationService.LastPageKey);
        var request = Assert.IsType<PromptLaunchRequest>(_navigationService.LastParameter);
        Assert.Equal("add a feature", request.PromptText);
        Assert.Equal("/saved/dir", request.WorkingDirectoryPath);
        Assert.False(request.AutoRun);
    }
}
