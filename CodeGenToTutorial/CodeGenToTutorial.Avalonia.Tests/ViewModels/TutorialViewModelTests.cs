using CodeGenToTutorial.Avalonia.Tests.Fakes;
using CodeGenToTutorial.Avalonia.ViewModels;
using CodeGenToTutorial.Core.ViewModels;

namespace CodeGenToTutorial.Avalonia.Tests.ViewModels;

public class TutorialViewModelTests
{
    private readonly FakePromptResultStore _promptResultStore = new();

    private TutorialViewModel CreateSut() => new(_promptResultStore);

    [Fact]
    public void HasUnsavedChanges_NoFiles_IsFalse()
    {
        var sut = CreateSut();
        sut.OnNavigatedTo(null!);

        Assert.False(sut.HasUnsavedChanges);
    }

    [Fact]
    public void HasUnsavedChanges_NoFileIsDirty_IsFalse()
    {
        _promptResultStore.SetResult("tutorial", [new ProposedFileChangeViewModel("a.txt", "proposed", "original")], "/tmp");
        var sut = CreateSut();
        sut.OnNavigatedTo(null!);

        Assert.False(sut.HasUnsavedChanges);
    }

    [Fact]
    public void HasUnsavedChanges_OneFileIsDirty_IsTrue()
    {
        _promptResultStore.SetResult("tutorial", [new ProposedFileChangeViewModel("a.txt", "proposed", "original")], "/tmp");
        var sut = CreateSut();
        sut.OnNavigatedTo(null!);

        sut.Files[0].EditedContent = "edited";

        Assert.True(sut.HasUnsavedChanges);
    }

    [Fact]
    public void HasUnsavedChanges_DirtyFileRevertedBackToProposed_IsFalseAgain()
    {
        _promptResultStore.SetResult("tutorial", [new ProposedFileChangeViewModel("a.txt", "proposed", "original")], "/tmp");
        var sut = CreateSut();
        sut.OnNavigatedTo(null!);
        sut.Files[0].EditedContent = "edited";

        sut.Files[0].RevertToProposedCommand.Execute(null);

        Assert.False(sut.HasUnsavedChanges);
    }
}
