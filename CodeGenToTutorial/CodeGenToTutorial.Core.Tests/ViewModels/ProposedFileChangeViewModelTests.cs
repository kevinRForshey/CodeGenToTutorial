using CodeGenToTutorial.Core.Models;
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

    [Fact]
    public void Constructor_ComputesInitialDiffHunksFromOriginalAndProposed()
    {
        var file = new ProposedFileChangeViewModel("a.txt", "line1\nline2", "line1");

        var hunk = Assert.Single(file.DiffHunks);
        Assert.Equal(file.DiffLines, hunk.Lines);
    }

    [Fact]
    public void Hunks_SetAfterConstruction_PopulatesMatchedDiffHunksFromInitialDiffHunks()
    {
        var hunk = new ProposedHunk("Add line2", string.Empty, "line2", new HunkLocation(1, 1));

        var file = new ProposedFileChangeViewModel("a.txt", "line1\nline2", "line1")
        {
            Hunks = new[] { hunk },
        };

        var match = Assert.Single(hunk.MatchedDiffHunks);
        Assert.Same(file.DiffHunks[0], match);
    }

    [Fact]
    public void EditedContent_Changed_RecomputesMatchedDiffHunksForExistingSteps()
    {
        var hunk = new ProposedHunk("Add line2", string.Empty, "line2", new HunkLocation(1, 1));

        var file = new ProposedFileChangeViewModel("a.txt", "line1\nline2", "line1")
        {
            Hunks = new[] { hunk },
        };
        Assert.Single(hunk.MatchedDiffHunks);

        // Editing the file back to match the original removes every diff hunk, so there's nothing
        // left for the step's location to match.
        file.EditedContent = "line1";

        Assert.Empty(hunk.MatchedDiffHunks);
    }

    [Fact]
    public void Hunks_StepLocationUnresolved_MatchedDiffHunksStaysEmpty()
    {
        var hunk = new ProposedHunk("Unresolvable step", string.Empty, "nonexistent snippet", null);

        var file = new ProposedFileChangeViewModel("a.txt", "line1\nline2", "line1")
        {
            Hunks = new[] { hunk },
        };

        Assert.Empty(hunk.MatchedDiffHunks);
    }

    [Fact]
    public void IsSelected_NoHunks_DefaultsToTrue()
    {
        var file = new ProposedFileChangeViewModel("a.txt", "proposed", "original");

        Assert.True(file.IsSelected);
        Assert.Equal("Whole file", file.SelectionSummary);
    }

    [Fact]
    public void IsSelected_NoHunks_SetFalse_UpdatesSummaryToExcluded()
    {
        var file = new ProposedFileChangeViewModel("a.txt", "proposed", "original")
        {
            IsSelected = false,
        };

        Assert.False(file.IsSelected);
        Assert.Equal("Excluded", file.SelectionSummary);
    }

    [Fact]
    public void IsSelected_AllHunksSelected_IsTrue()
    {
        var file = new ProposedFileChangeViewModel("a.txt", "proposed", "original")
        {
            Hunks = new[]
            {
                new ProposedHunk("Step 1", string.Empty, "x", null),
                new ProposedHunk("Step 2", string.Empty, "y", null),
            },
        };

        Assert.True(file.IsSelected);
        Assert.Equal("2/2 selected", file.SelectionSummary);
    }

    [Fact]
    public void IsSelected_NoHunksSelected_IsFalse()
    {
        var file = new ProposedFileChangeViewModel("a.txt", "proposed", "original")
        {
            Hunks = new[]
            {
                new ProposedHunk("Step 1", string.Empty, "x", null) { IsSelected = false },
                new ProposedHunk("Step 2", string.Empty, "y", null) { IsSelected = false },
            },
        };

        Assert.False(file.IsSelected);
        Assert.Equal("0/2 selected", file.SelectionSummary);
    }

    [Fact]
    public void IsSelected_MixedHunkSelection_IsNullIndeterminate()
    {
        var file = new ProposedFileChangeViewModel("a.txt", "proposed", "original")
        {
            Hunks = new[]
            {
                new ProposedHunk("Step 1", string.Empty, "x", null),
                new ProposedHunk("Step 2", string.Empty, "y", null) { IsSelected = false },
            },
        };

        Assert.Null(file.IsSelected);
        Assert.Equal("1/2 selected", file.SelectionSummary);
    }

    [Fact]
    public void IsSelected_SetTrue_SelectsEveryHunk()
    {
        var hunks = new[]
        {
            new ProposedHunk("Step 1", string.Empty, "x", null) { IsSelected = false },
            new ProposedHunk("Step 2", string.Empty, "y", null) { IsSelected = false },
        };
        var file = new ProposedFileChangeViewModel("a.txt", "proposed", "original") { Hunks = hunks };

        file.IsSelected = true;

        Assert.All(hunks, h => Assert.True(h.IsSelected));
    }

    [Fact]
    public void IsSelected_SetFalse_DeselectsEveryHunk()
    {
        var hunks = new[]
        {
            new ProposedHunk("Step 1", string.Empty, "x", null),
            new ProposedHunk("Step 2", string.Empty, "y", null),
        };
        var file = new ProposedFileChangeViewModel("a.txt", "proposed", "original") { Hunks = hunks };

        file.IsSelected = false;

        Assert.All(hunks, h => Assert.False(h.IsSelected));
    }

    [Fact]
    public void IsSelected_SetNull_TreatsAsSelectAll()
    {
        var hunks = new[]
        {
            new ProposedHunk("Step 1", string.Empty, "x", null) { IsSelected = false },
            new ProposedHunk("Step 2", string.Empty, "y", null) { IsSelected = false },
        };
        var file = new ProposedFileChangeViewModel("a.txt", "proposed", "original") { Hunks = hunks };

        file.IsSelected = null;

        Assert.All(hunks, h => Assert.True(h.IsSelected));
    }

    [Fact]
    public void HunkPropertyChanged_TogglingOneStep_UpdatesRollupWithoutReassigningHunks()
    {
        var hunks = new[]
        {
            new ProposedHunk("Step 1", string.Empty, "x", null),
            new ProposedHunk("Step 2", string.Empty, "y", null),
        };
        var file = new ProposedFileChangeViewModel("a.txt", "proposed", "original") { Hunks = hunks };

        hunks[0].IsSelected = false;

        Assert.Null(file.IsSelected);
        Assert.Equal(1, file.SelectedHunkCount);
    }

    [Fact]
    public void HasSelectableHunks_NoHunks_IsFalse()
    {
        var file = new ProposedFileChangeViewModel("a.txt", "proposed", "original");

        Assert.False(file.HasSelectableHunks);
    }

    [Fact]
    public void HasSelectableHunks_WithHunks_IsTrue()
    {
        var file = new ProposedFileChangeViewModel("a.txt", "proposed", "original")
        {
            Hunks = new[] { new ProposedHunk("Step 1", string.Empty, "x", null) },
        };

        Assert.True(file.HasSelectableHunks);
    }

    [Fact]
    public void SelectAllHunksCommand_NoHunks_CannotExecute()
    {
        var file = new ProposedFileChangeViewModel("a.txt", "proposed", "original");

        Assert.False(file.SelectAllHunksCommand.CanExecute(null));
        Assert.False(file.SelectNoHunksCommand.CanExecute(null));
        Assert.False(file.InvertHunkSelectionCommand.CanExecute(null));
    }

    [Fact]
    public void SelectAllHunksCommand_SomeDeselected_SelectsAll()
    {
        var hunks = new[]
        {
            new ProposedHunk("Step 1", string.Empty, "x", null) { IsSelected = false },
            new ProposedHunk("Step 2", string.Empty, "y", null),
        };
        var file = new ProposedFileChangeViewModel("a.txt", "proposed", "original") { Hunks = hunks };

        file.SelectAllHunksCommand.Execute(null);

        Assert.All(hunks, h => Assert.True(h.IsSelected));
    }

    [Fact]
    public void SelectNoHunksCommand_SomeSelected_DeselectsAll()
    {
        var hunks = new[]
        {
            new ProposedHunk("Step 1", string.Empty, "x", null),
            new ProposedHunk("Step 2", string.Empty, "y", null),
        };
        var file = new ProposedFileChangeViewModel("a.txt", "proposed", "original") { Hunks = hunks };

        file.SelectNoHunksCommand.Execute(null);

        Assert.All(hunks, h => Assert.False(h.IsSelected));
    }

    [Fact]
    public void InvertHunkSelectionCommand_FlipsEachHunk()
    {
        var hunks = new[]
        {
            new ProposedHunk("Step 1", string.Empty, "x", null),
            new ProposedHunk("Step 2", string.Empty, "y", null) { IsSelected = false },
        };
        var file = new ProposedFileChangeViewModel("a.txt", "proposed", "original") { Hunks = hunks };

        file.InvertHunkSelectionCommand.Execute(null);

        Assert.False(hunks[0].IsSelected);
        Assert.True(hunks[1].IsSelected);
    }

    [Fact]
    public void HasAnySelectedChange_NoHunks_WholeFileSelected_IsTrue()
    {
        var file = new ProposedFileChangeViewModel("a.txt", "proposed", "original");

        Assert.True(file.HasAnySelectedChange);
    }

    [Fact]
    public void HasAnySelectedChange_NoHunks_WholeFileExcluded_IsFalse()
    {
        var file = new ProposedFileChangeViewModel("a.txt", "proposed", "original") { IsSelected = false };

        Assert.False(file.HasAnySelectedChange);
    }

    [Fact]
    public void HasAnySelectedChange_SomeHunksSelected_IsTrue()
    {
        var hunks = new[]
        {
            new ProposedHunk("Step 1", string.Empty, "x", null) { IsSelected = false },
            new ProposedHunk("Step 2", string.Empty, "y", null),
        };
        var file = new ProposedFileChangeViewModel("a.txt", "proposed", "original") { Hunks = hunks };

        Assert.True(file.HasAnySelectedChange);
    }

    [Fact]
    public void HasAnySelectedChange_AllHunksDeselected_IsFalse()
    {
        var hunks = new[]
        {
            new ProposedHunk("Step 1", string.Empty, "x", null) { IsSelected = false },
            new ProposedHunk("Step 2", string.Empty, "y", null) { IsSelected = false },
        };
        var file = new ProposedFileChangeViewModel("a.txt", "proposed", "original") { Hunks = hunks };

        Assert.False(file.HasAnySelectedChange);
    }

    [Fact]
    public void BuildSelectedContent_NoHunks_WholeFileSelected_ReturnsEditedContent()
    {
        var file = new ProposedFileChangeViewModel("a.txt", "proposed", "original") { EditedContent = "edited" };

        Assert.Equal("edited", file.BuildSelectedContent());
    }

    [Fact]
    public void BuildSelectedContent_NoHunks_WholeFileExcluded_ReturnsOriginalContent()
    {
        var file = new ProposedFileChangeViewModel("a.txt", "proposed", "original")
        {
            EditedContent = "edited",
            IsSelected = false,
        };

        Assert.Equal("original", file.BuildSelectedContent());
    }

    [Fact]
    public void BuildSelectedContent_MixedHunkSelection_AppliesOnlySelectedSteps()
    {
        // Two changes far enough apart that GroupIntoHunks keeps them as separate DiffHunks, each
        // matched to its own step.
        var gap = string.Join('\n', Enumerable.Range(0, 20).Select(i => $"gap-{i}"));
        var original = $"OLD1\n{gap}\nOLD2";
        var proposed = $"NEW1\n{gap}\nNEW2";
        var proposedLineCount = proposed.Split('\n').Length;
        var step1 = new ProposedHunk("Step 1", string.Empty, "NEW1", new HunkLocation(0, 0)) { IsSelected = false };
        var step2 = new ProposedHunk("Step 2", string.Empty, "NEW2", new HunkLocation(proposedLineCount - 1, proposedLineCount - 1));

        var file = new ProposedFileChangeViewModel("a.txt", proposed, original) { Hunks = new[] { step1, step2 } };

        Assert.Equal($"OLD1\n{gap}\nNEW2", file.BuildSelectedContent());
    }

    [Fact]
    public void BuildSelectedContent_DiffHunkUnmatchedByAnyStep_DefaultsToIncluded()
    {
        // The step's location doesn't resolve to anywhere in the proposed content, so the actual
        // DiffHunk the edit produced has nothing checked to exclude it - it should still be written.
        var step = new ProposedHunk("Unresolvable step", string.Empty, "nonexistent snippet", null) { IsSelected = false };
        var file = new ProposedFileChangeViewModel("a.txt", "new", "old") { Hunks = new[] { step } };

        Assert.Equal("new", file.BuildSelectedContent());
    }

    [Fact]
    public void MarkHunksAppliedFromSelection_SetsEachHunksIsAppliedToItsCurrentIsSelected()
    {
        var hunks = new[]
        {
            new ProposedHunk("Step 1", string.Empty, "x", null),
            new ProposedHunk("Step 2", string.Empty, "y", null) { IsSelected = false },
        };
        var file = new ProposedFileChangeViewModel("a.txt", "proposed", "original") { Hunks = hunks };

        file.MarkHunksAppliedFromSelection();

        Assert.True(hunks[0].IsApplied);
        Assert.False(hunks[1].IsApplied);
    }

    [Fact]
    public void MarkHunksAppliedFromSelection_NoHunks_IsNoOp()
    {
        var file = new ProposedFileChangeViewModel("a.txt", "proposed", "original");

        file.MarkHunksAppliedFromSelection();
    }

    [Fact]
    public void EditedContent_ChangedAfterApply_InvalidatesEveryHunksIsApplied()
    {
        var hunk = new ProposedHunk("Step 1", string.Empty, "line2", new HunkLocation(1, 1));
        var file = new ProposedFileChangeViewModel("a.txt", "line1\nline2", "line1") { Hunks = new[] { hunk } };
        file.MarkHunksAppliedFromSelection();
        Assert.True(hunk.IsApplied);

        file.EditedContent = "line1\nline2\nline3";

        Assert.False(hunk.IsApplied);
    }

    [Fact]
    public void RefreshOriginalContent_ReadsCurrentDiskContentAndRecomputesDiff()
    {
        var workingDirectory = Directory.CreateTempSubdirectory("CodeGenToTutorialTests_").FullName;
        try
        {
            File.WriteAllText(Path.Combine(workingDirectory, "a.txt"), "changed on disk");
            var file = new ProposedFileChangeViewModel("a.txt", "proposed", "original");

            file.RefreshOriginalContent(workingDirectory);

            Assert.Equal("changed on disk", file.OriginalContent);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void RefreshOriginalContent_FileDoesNotExistYet_SetsEmptyOriginalContent()
    {
        var workingDirectory = Directory.CreateTempSubdirectory("CodeGenToTutorialTests_").FullName;
        try
        {
            var file = new ProposedFileChangeViewModel("new-file.txt", "proposed", "original");

            file.RefreshOriginalContent(workingDirectory);

            Assert.Equal(string.Empty, file.OriginalContent);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void RefreshOriginalContent_NoWorkingDirectory_SetsEmptyOriginalContent()
    {
        var file = new ProposedFileChangeViewModel("a.txt", "proposed", "original");

        file.RefreshOriginalContent(string.Empty);

        Assert.Equal(string.Empty, file.OriginalContent);
    }
}
