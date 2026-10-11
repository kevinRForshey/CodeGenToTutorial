using CodeGenToTutorial.Avalonia.Tests.Fakes;
using CodeGenToTutorial.Avalonia.ViewModels;
using CodeGenToTutorial.Core.ViewModels;

namespace CodeGenToTutorial.Avalonia.Tests.ViewModels;

public class DiffsViewModelTests
{
    private readonly FakePromptResultStore _promptResultStore = new();

    private DiffsViewModel CreateSut() => new(_promptResultStore);

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
}
