using Paarde.Avalonia.Models;
using Paarde.Avalonia.Tests.Fakes;
using Paarde.Avalonia.ViewModels;
using Paarde.Core.Models;
using Paarde.Core.ViewModels;

namespace Paarde.Avalonia.Tests.ViewModels;

public class DiffsViewModelTests
{
    private readonly FakePromptResultStore _promptResultStore = new();
    private readonly FakeNavigationService _navigationService = new();

    private DiffsViewModel CreateSut() => new(_promptResultStore, _navigationService);

    [Fact]
    public void HasNoFiles_NoPromptRunYet_IsTrue()
    {
        var sut = CreateSut();
        sut.OnNavigatedTo(null!);

        Assert.True(sut.HasNoFiles);
        Assert.False(sut.HasFilesButNoneSelected);
    }

    [Fact]
    public void HasFilesButNoneSelected_FilesPresent_IsFalseOnceOnNavigatedToAutoSelectsTheFirstOne()
    {
        _promptResultStore.SetResult("tutorial", [new ProposedFileChangeViewModel("a.txt", "proposed", "original")], "/tmp");
        var sut = CreateSut();

        sut.OnNavigatedTo(null!);

        // OnNavigatedTo already calls EnsureItemSelected, so with files present there's always a
        // selection by the time the page is shown - HasFilesButNoneSelected only guards the moment
        // between the two, or a Selected that's been explicitly cleared.
        Assert.False(sut.HasNoFiles);
        Assert.False(sut.HasFilesButNoneSelected);
    }

    [Fact]
    public void HasFilesButNoneSelected_FilesPresentButSelectionCleared_IsTrue()
    {
        _promptResultStore.SetResult("tutorial", [new ProposedFileChangeViewModel("a.txt", "proposed", "original")], "/tmp");
        var sut = CreateSut();
        sut.OnNavigatedTo(null!);

        sut.Selected = null;

        Assert.True(sut.HasFilesButNoneSelected);
    }

    [Fact]
    public void OnNavigatedTo_DiffJumpRequest_SelectsTheRequestedFileAndExposesReturnToStep()
    {
        var hunk = new ProposedHunk("Add line2", string.Empty, "line2", new HunkLocation(1, 1));
        var file = new ProposedFileChangeViewModel("a.txt", "line1\nline2", "line1") { Hunks = new[] { hunk } };
        var otherFile = new ProposedFileChangeViewModel("b.txt", "proposed", "original");
        _promptResultStore.SetResult("tutorial", [otherFile, file], "/tmp");
        var sut = CreateSut();

        sut.OnNavigatedTo(new DiffJumpRequest(file, file.DiffHunks[0], hunk));

        Assert.Same(file, sut.Selected);
        Assert.Same(hunk, sut.ReturnToStep);
        Assert.True(sut.BackToTutorialCommand.CanExecute(null));
    }

    [Fact]
    public void OnNavigatedTo_PlainNavigation_LeavesReturnToStepEmpty()
    {
        var hunk = new ProposedHunk("Add line2", string.Empty, "line2", new HunkLocation(1, 1));
        var file = new ProposedFileChangeViewModel("a.txt", "line1\nline2", "line1") { Hunks = new[] { hunk } };
        _promptResultStore.SetResult("tutorial", [file], "/tmp");
        var sut = CreateSut();
        sut.OnNavigatedTo(new DiffJumpRequest(file, file.DiffHunks[0], hunk));

        sut.OnNavigatedTo(null!);

        Assert.Null(sut.ReturnToStep);
        Assert.False(sut.BackToTutorialCommand.CanExecute(null));
    }

    [Fact]
    public void BackToTutorialCommand_Executed_NavigatesToTutorialWithTheOriginStep()
    {
        var hunk = new ProposedHunk("Add line2", string.Empty, "line2", new HunkLocation(1, 1));
        var file = new ProposedFileChangeViewModel("a.txt", "line1\nline2", "line1") { Hunks = new[] { hunk } };
        _promptResultStore.SetResult("tutorial", [file], "/tmp");
        var sut = CreateSut();
        sut.OnNavigatedTo(new DiffJumpRequest(file, file.DiffHunks[0], hunk));

        sut.BackToTutorialCommand.Execute(null);

        var request = Assert.IsType<TutorialJumpRequest>(_navigationService.LastParameter);
        Assert.Same(file, request.File);
        Assert.Same(hunk, request.TargetStep);
    }
}
