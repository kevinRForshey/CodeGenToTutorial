using CodeGenToTutorial.Core.Models;
using CodeGenToTutorial.Core.Services;
using CodeGenToTutorial.Core.ViewModels;

namespace CodeGenToTutorial.Core.Tests.Services;

public class ApplyOrchestrationServiceTests : IDisposable
{
    private readonly string _workingDirectory;
    private readonly ApplyOrchestrationService _service = new(new FileChangeApplyService());

    public ApplyOrchestrationServiceTests()
    {
        _workingDirectory = Directory.CreateTempSubdirectory("CodeGenToTutorialTests_").FullName;
    }

    public void Dispose()
    {
        if (Directory.Exists(_workingDirectory))
        {
            Directory.Delete(_workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void Preview_NoFilesSelected_ReturnsZero()
    {
        var files = new[] { new ProposedFileChangeViewModel("a.txt", "proposed", "original") { IsSelected = false } };

        var preview = _service.Preview(files);

        Assert.Equal(new ApplyPreview(0, 0), preview);
    }

    [Fact]
    public void Preview_WholeFileFallbackSelected_CountsAsOneFileOneChange()
    {
        var files = new[] { new ProposedFileChangeViewModel("a.txt", "proposed", "original") };

        var preview = _service.Preview(files);

        Assert.Equal(new ApplyPreview(1, 1), preview);
    }

    [Fact]
    public void Preview_MixOfFilesWithSomeHunksSelected_CountsOnlySelectedHunks()
    {
        var hunks = new[]
        {
            new ProposedHunk("Step 1", string.Empty, "x", null),
            new ProposedHunk("Step 2", string.Empty, "y", null) { IsSelected = false },
        };
        var fileWithHunks = new ProposedFileChangeViewModel("a.txt", "proposed", "original") { Hunks = hunks };
        var fileExcluded = new ProposedFileChangeViewModel("b.txt", "proposed", "original") { IsSelected = false };

        var preview = _service.Preview(new[] { fileWithHunks, fileExcluded });

        Assert.Equal(new ApplyPreview(1, 1), preview);
    }

    [Fact]
    public void ApplySelected_NothingSelected_WritesNothingAndReturnsNoResults()
    {
        var file = new ProposedFileChangeViewModel("a.txt", "proposed", "original") { IsSelected = false };

        var result = _service.ApplySelected(new[] { file }, _workingDirectory);

        Assert.Empty(result.Results);
        Assert.False(File.Exists(Path.Combine(_workingDirectory, "a.txt")));
    }

    [Fact]
    public void ApplySelected_NewFile_WritesItAndReportsApplied()
    {
        var file = new ProposedFileChangeViewModel("a.txt", "proposed content");

        var result = _service.ApplySelected(new[] { file }, _workingDirectory);

        Assert.Equal(1, result.AppliedCount);
        Assert.Equal("proposed content", File.ReadAllText(Path.Combine(_workingDirectory, "a.txt")));
    }

    [Fact]
    public void ApplySelected_MarksHunksAppliedAfterSuccessfulWrite()
    {
        File.WriteAllText(Path.Combine(_workingDirectory, "a.txt"), "line1");
        var hunk = new ProposedHunk("Step 1", string.Empty, "line2", new HunkLocation(1, 1));
        var file = new ProposedFileChangeViewModel("a.txt", "line1\nline2", "line1") { Hunks = new[] { hunk } };

        _service.ApplySelected(new[] { file }, _workingDirectory);

        Assert.True(hunk.IsApplied);
    }

    [Fact]
    public void ApplySelected_FileUnchangedSinceProposal_WritesAndReportsApplied()
    {
        File.WriteAllText(Path.Combine(_workingDirectory, "a.txt"), "original");
        var file = new ProposedFileChangeViewModel("a.txt", "proposed", "original");

        var result = _service.ApplySelected(new[] { file }, _workingDirectory);

        Assert.Equal(1, result.AppliedCount);
        Assert.Equal("proposed", File.ReadAllText(Path.Combine(_workingDirectory, "a.txt")));
    }

    [Fact]
    public void ApplySelected_FileChangedOnDiskSinceProposal_ReportsConflictAndDoesNotWrite()
    {
        File.WriteAllText(Path.Combine(_workingDirectory, "a.txt"), "changed on disk");
        var file = new ProposedFileChangeViewModel("a.txt", "proposed", "original");

        var result = _service.ApplySelected(new[] { file }, _workingDirectory);

        Assert.Equal(1, result.ConflictCount);
        Assert.Equal("changed on disk", File.ReadAllText(Path.Combine(_workingDirectory, "a.txt")));
    }

    [Fact]
    public void ApplySelected_OneFileConflictsAnotherDoesNot_StillAppliesTheOther()
    {
        File.WriteAllText(Path.Combine(_workingDirectory, "conflicted.txt"), "changed on disk");
        var conflicted = new ProposedFileChangeViewModel("conflicted.txt", "proposed", "original");
        var clean = new ProposedFileChangeViewModel("clean.txt", "proposed content");

        var result = _service.ApplySelected(new[] { conflicted, clean }, _workingDirectory);

        Assert.Equal(1, result.ConflictCount);
        Assert.Equal(1, result.AppliedCount);
        Assert.True(File.Exists(Path.Combine(_workingDirectory, "clean.txt")));
    }

    [Fact]
    public void ApplySelected_PathTraversal_ReportsFailedAndDoesNotEscapeTheBatch()
    {
        var malicious = new ProposedFileChangeViewModel("../escape.txt", "malicious");
        var clean = new ProposedFileChangeViewModel("clean.txt", "proposed content");

        var result = _service.ApplySelected(new[] { malicious, clean }, _workingDirectory);

        Assert.Equal(1, result.FailedCount);
        Assert.Equal(1, result.AppliedCount);
        Assert.NotNull(Assert.Single(result.FailedFiles).ErrorMessage);
    }
}
