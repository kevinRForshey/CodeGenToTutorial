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

    [Fact]
    public void RevertToProposedCommand_NotDirty_CannotExecute()
    {
        var file = new ProposedFileChangeViewModel("a.txt", "proposed");

        Assert.False(file.RevertToProposedCommand.CanExecute(null));
    }

    [Fact]
    public void RevertToProposedCommand_Dirty_CanExecuteAndRestoresProposedContent()
    {
        var file = new ProposedFileChangeViewModel("a.txt", "proposed")
        {
            EditedContent = "edited by the user",
        };

        Assert.True(file.RevertToProposedCommand.CanExecute(null));

        file.RevertToProposedCommand.Execute(null);

        Assert.Equal("proposed", file.EditedContent);
        Assert.False(file.IsDirty);
    }

    [Fact]
    public void RevertToProposedCommand_AfterReverting_CanNoLongerExecute()
    {
        var file = new ProposedFileChangeViewModel("a.txt", "proposed")
        {
            EditedContent = "edited by the user",
        };

        file.RevertToProposedCommand.Execute(null);

        Assert.False(file.RevertToProposedCommand.CanExecute(null));
    }

    [Fact]
    public void RevertToOriginalCommand_EditedContentEqualsOriginal_CannotExecute()
    {
        var file = new ProposedFileChangeViewModel("a.txt", "proposed", "proposed");

        Assert.False(file.RevertToOriginalCommand.CanExecute(null));
    }

    [Fact]
    public void RevertToOriginalCommand_ProposedDiffersFromOriginal_CanExecuteEvenWhenNotDirty()
    {
        // Not dirty (EditedContent == ProposedContent) but the proposed change itself still differs
        // from what's on disk - rejecting the whole proposal outright should still be available.
        var file = new ProposedFileChangeViewModel("a.txt", "proposed", "original");

        Assert.False(file.IsDirty);
        Assert.True(file.RevertToOriginalCommand.CanExecute(null));
    }

    [Fact]
    public void RevertToOriginalCommand_CanExecuteAndRestoresOriginalContent()
    {
        var file = new ProposedFileChangeViewModel("a.txt", "proposed", "original")
        {
            EditedContent = "edited by the user",
        };

        file.RevertToOriginalCommand.Execute(null);

        Assert.Equal("original", file.EditedContent);
        Assert.True(file.IsDirty);
    }

    [Fact]
    public void RevertToOriginalCommand_AfterReverting_CanNoLongerExecute()
    {
        var file = new ProposedFileChangeViewModel("a.txt", "proposed", "original")
        {
            EditedContent = "edited by the user",
        };

        file.RevertToOriginalCommand.Execute(null);

        Assert.False(file.RevertToOriginalCommand.CanExecute(null));
    }

    [Fact]
    public void OriginalContent_ChangedToMatchEditedContent_RevertToOriginalCommandCanNoLongerExecute()
    {
        var file = new ProposedFileChangeViewModel("a.txt", "proposed", "original");
        Assert.True(file.RevertToOriginalCommand.CanExecute(null));

        file.OriginalContent = "proposed";

        Assert.False(file.RevertToOriginalCommand.CanExecute(null));
    }
}
