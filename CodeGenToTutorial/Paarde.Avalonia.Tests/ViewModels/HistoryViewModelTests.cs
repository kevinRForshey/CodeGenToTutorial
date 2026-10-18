using Paarde.Avalonia.Models;
using Paarde.Avalonia.Tests.Fakes;
using Paarde.Avalonia.ViewModels;
using Paarde.Core.Models;

namespace Paarde.Avalonia.Tests.ViewModels;

public class HistoryViewModelTests
{
    private readonly FakeRunSessionStore _runSessionStore = new();
    private readonly FakeNavigationService _navigationService = new();

    private HistoryViewModel CreateSut() => new(_runSessionStore, _navigationService);

    private static RunSession CreateSession(string userPromptText, DateTimeOffset startedAt, IReadOnlyList<RunSessionFile>? files = null) => new(
        Guid.NewGuid().ToString("N"),
        startedAt,
        "/working/dir",
        "composed prompt",
        "raw output",
        "tutorial text",
        files ?? Array.Empty<RunSessionFile>(),
        Array.Empty<ApplyHistoryEntry>(),
        userPromptText);

    private static RunSessionFile CreateFile(string filePath) =>
        new(filePath, "original", "proposed", "proposed", true, Array.Empty<RunSessionHunk>());

    [Fact]
    public void OnNavigatedTo_NoSessions_HasNoSessionsAndNothingSelected()
    {
        var sut = CreateSut();

        sut.OnNavigatedTo(null!);

        Assert.True(sut.HasNoSessions);
        Assert.Null(sut.Selected);
    }

    [Fact]
    public void OnNavigatedTo_PopulatesSessionsFromListSessions()
    {
        _runSessionStore.SessionsToList = new[]
        {
            CreateSession("second prompt", DateTimeOffset.UtcNow),
            CreateSession("first prompt", DateTimeOffset.UtcNow.AddMinutes(-1)),
        };
        var sut = CreateSut();

        sut.OnNavigatedTo(null!);

        Assert.Equal(2, sut.Sessions.Count);
        Assert.False(sut.HasNoSessions);
    }

    [Fact]
    public void OnNavigatedTo_AutoSelectsTheFirstSession()
    {
        var session = CreateSession("only prompt", DateTimeOffset.UtcNow);
        _runSessionStore.SessionsToList = new[] { session };
        var sut = CreateSut();

        sut.OnNavigatedTo(null!);

        Assert.NotNull(sut.Selected);
        Assert.Equal(session, sut.Selected!.Session);
    }

    [Fact]
    public void SelectingASession_PopulatesSelectedFilesFromItsFiles()
    {
        var session = CreateSession("prompt", DateTimeOffset.UtcNow, new[] { CreateFile("a.cs"), CreateFile("b.cs") });
        _runSessionStore.SessionsToList = new[] { session };
        var sut = CreateSut();

        sut.OnNavigatedTo(null!);

        Assert.Equal(2, sut.SelectedFiles.Count);
        Assert.Equal("a.cs", sut.SelectedFile!.FilePath);
        Assert.False(sut.SelectedHasNoFiles);
    }

    [Fact]
    public void SelectingASessionWithNoFiles_SelectedHasNoFilesIsTrue()
    {
        _runSessionStore.SessionsToList = new[] { CreateSession("prompt", DateTimeOffset.UtcNow) };
        var sut = CreateSut();

        sut.OnNavigatedTo(null!);

        Assert.Empty(sut.SelectedFiles);
        Assert.True(sut.SelectedHasNoFiles);
    }

    [Fact]
    public void NoSessionSelected_CommandsCannotExecute()
    {
        var sut = CreateSut();

        sut.OnNavigatedTo(null!);

        Assert.False(sut.ForkCommand.CanExecute(null));
        Assert.False(sut.RerunCommand.CanExecute(null));
    }

    [Fact]
    public void ForkCommand_NavigatesToPromptPageWithAutoRunFalse()
    {
        _runSessionStore.SessionsToList = new[] { CreateSession("fork this prompt", DateTimeOffset.UtcNow) };
        var sut = CreateSut();
        sut.OnNavigatedTo(null!);

        sut.ForkCommand.Execute(null);

        Assert.Equal(1, _navigationService.NavigateToCallCount);
        Assert.Equal(typeof(PromptViewModel).FullName, _navigationService.LastPageKey);
        var request = Assert.IsType<PromptLaunchRequest>(_navigationService.LastParameter);
        Assert.Equal("fork this prompt", request.PromptText);
        Assert.Equal("/working/dir", request.WorkingDirectoryPath);
        Assert.False(request.AutoRun);
    }

    [Fact]
    public void RerunCommand_NavigatesToPromptPageWithAutoRunTrue()
    {
        _runSessionStore.SessionsToList = new[] { CreateSession("rerun this prompt", DateTimeOffset.UtcNow) };
        var sut = CreateSut();
        sut.OnNavigatedTo(null!);

        sut.RerunCommand.Execute(null);

        var request = Assert.IsType<PromptLaunchRequest>(_navigationService.LastParameter);
        Assert.True(request.AutoRun);
    }
}
