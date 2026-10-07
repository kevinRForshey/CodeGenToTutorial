using CodeGenToTutorial.Core.ViewModels;

namespace CodeGenToTutorial.Core.Tests.ViewModels;

public class ProposedFileChangeViewModelTests
{
    [Fact]
    public void Constructor_SetsOriginalProposedAndEditedContent()
    {
        var file = new ProposedFileChangeViewModel("a.txt", "proposed", "original");

        Assert.Equal("a.txt", file.FilePath);
        Assert.Equal("original", file.OriginalContent);
        Assert.Equal("proposed", file.ProposedContent);
        Assert.Equal("proposed", file.EditedContent);
        Assert.False(file.IsDirty);
    }

    [Fact]
    public void Constructor_NoOriginalContentGiven_DefaultsToEmpty()
    {
        var file = new ProposedFileChangeViewModel("a.txt", "proposed");

        Assert.Equal(string.Empty, file.OriginalContent);
    }

    [Fact]
    public void EditedContent_ChangedFromProposed_SetsIsDirty()
    {
        var file = new ProposedFileChangeViewModel("a.txt", "proposed");

        file.EditedContent = "edited";

        Assert.True(file.IsDirty);
    }

    [Fact]
    public void EditedContent_SetBackToProposed_ClearsIsDirty()
    {
        var file = new ProposedFileChangeViewModel("a.txt", "proposed")
        {
            EditedContent = "edited",
        };

        file.EditedContent = "proposed";

        Assert.False(file.IsDirty);
    }
}
