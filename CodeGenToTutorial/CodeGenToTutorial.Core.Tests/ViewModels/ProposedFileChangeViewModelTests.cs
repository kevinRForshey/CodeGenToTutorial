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
    public void Constructor_ComputesInitialDiffLinesFromOriginalAndProposed()
    {
        var file = new ProposedFileChangeViewModel("a.txt", "line1\nline2", "line1");

        Assert.Equal(2, file.DiffLines.Count);
    }

    [Fact]
    public void EditedContent_Changed_RecomputesDiffLines()
    {
        var file = new ProposedFileChangeViewModel("a.txt", "line1", "line1");
        Assert.Single(file.DiffLines);

        file.EditedContent = "line1\nline2";

        Assert.Equal(2, file.DiffLines.Count);
    }

    [Fact]
    public void OriginalContent_Changed_RecomputesDiffLinesAgainstEditedContent()
    {
        var file = new ProposedFileChangeViewModel("a.txt", "line1\nline2")
        {
            EditedContent = "line1\nline2\nline3",
        };

        file.OriginalContent = "line1\nline2";

        Assert.Equal(3, file.DiffLines.Count);
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
