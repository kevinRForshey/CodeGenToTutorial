using Paarde.Core.Helpers;
using Paarde.Core.Models;
using Paarde.Core.ViewModels;

namespace Paarde.Core.Tests.Helpers;

public class RunSessionFileConverterTests
{
    [Fact]
    public void ToRunSessionFile_CapturesContentAndWholeFileSelection()
    {
        var file = new ProposedFileChangeViewModel("a.cs", "proposed", "original") { EditedContent = "edited", IsSelected = false };

        var persisted = RunSessionFileConverter.ToRunSessionFile(file);

        Assert.Equal("a.cs", persisted.FilePath);
        Assert.Equal("original", persisted.OriginalContent);
        Assert.Equal("proposed", persisted.ProposedContent);
        Assert.Equal("edited", persisted.EditedContent);
        Assert.False(persisted.WholeFileSelected);
        Assert.Empty(persisted.Hunks);
    }

    [Fact]
    public void ToRunSessionFile_CapturesHunkTitleAndSelection()
    {
        var file = new ProposedFileChangeViewModel("a.cs", "proposed", "original")
        {
            Hunks = new[] { new ProposedHunk("Add Foo", "Explains Foo", "class Foo {}", new HunkLocation(0, 2)) { IsSelected = false } },
        };

        var persisted = RunSessionFileConverter.ToRunSessionFile(file);

        var hunk = Assert.Single(persisted.Hunks);
        Assert.Equal("Add Foo", hunk.Title);
        Assert.False(hunk.IsSelected);
    }

    [Fact]
    public void ToProposedFileChangeViewModel_IsTheInverseOfToRunSessionFile()
    {
        var original = new ProposedFileChangeViewModel("a.cs", "proposed", "original")
        {
            EditedContent = "edited",
            Hunks = new[] { new ProposedHunk("Add Foo", "Explains Foo", "class Foo {}", new HunkLocation(0, 2)) { IsSelected = false } },
        };

        var restored = RunSessionFileConverter.ToProposedFileChangeViewModel(RunSessionFileConverter.ToRunSessionFile(original));

        Assert.Equal(original.FilePath, restored.FilePath);
        Assert.Equal(original.OriginalContent, restored.OriginalContent);
        Assert.Equal(original.ProposedContent, restored.ProposedContent);
        Assert.Equal(original.EditedContent, restored.EditedContent);
        var restoredHunk = Assert.Single(restored.Hunks);
        Assert.Equal("Add Foo", restoredHunk.Title);
        Assert.False(restoredHunk.IsSelected);
    }

    [Fact]
    public void ToProposedFileChangeViewModel_NoHunks_RestoresWholeFileFallbackSelection()
    {
        var original = new ProposedFileChangeViewModel("a.cs", "proposed", "original") { IsSelected = false };

        var restored = RunSessionFileConverter.ToProposedFileChangeViewModel(RunSessionFileConverter.ToRunSessionFile(original));

        Assert.Empty(restored.Hunks);
        Assert.False(restored.IsSelected);
    }
}
