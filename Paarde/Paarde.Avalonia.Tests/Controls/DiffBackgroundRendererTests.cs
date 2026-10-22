using Paarde.Avalonia.Controls;
using Paarde.Core.Models;

namespace Paarde.Avalonia.Tests.Controls;

public class DiffBackgroundRendererTests
{
    [Fact]
    public void ComputeBands_AllUnchanged_ReturnsNoBands()
    {
        var diffLines = new[]
        {
            new DiffLine(DiffLineKind.Unchanged, "a"),
            new DiffLine(DiffLineKind.Unchanged, "b"),
        };

        var bands = DiffBackgroundRenderer.ComputeBands(diffLines);

        Assert.Empty(bands);
    }

    [Fact]
    public void ComputeBands_SingleAddedLine_ReturnsOneBandAtItsProposedLineNumber()
    {
        var diffLines = new[]
        {
            new DiffLine(DiffLineKind.Unchanged, "a"),
            new DiffLine(DiffLineKind.Added, "b"),
            new DiffLine(DiffLineKind.Unchanged, "c"),
        };

        var bands = DiffBackgroundRenderer.ComputeBands(diffLines);

        var band = Assert.Single(bands);
        Assert.Equal(new DiffBand(StartLine: 2, LineCount: 1, IsAddition: true), band);
    }

    [Fact]
    public void ComputeBands_ConsecutiveAddedLines_CoalesceIntoOneBand()
    {
        var diffLines = new[]
        {
            new DiffLine(DiffLineKind.Unchanged, "a"),
            new DiffLine(DiffLineKind.Added, "b"),
            new DiffLine(DiffLineKind.Added, "c"),
            new DiffLine(DiffLineKind.Added, "d"),
            new DiffLine(DiffLineKind.Unchanged, "e"),
        };

        var bands = DiffBackgroundRenderer.ComputeBands(diffLines);

        var band = Assert.Single(bands);
        Assert.Equal(new DiffBand(StartLine: 2, LineCount: 3, IsAddition: true), band);
    }

    [Fact]
    public void ComputeBands_TrailingAddedRun_ClosesAtEndOfFile()
    {
        var diffLines = new[]
        {
            new DiffLine(DiffLineKind.Unchanged, "a"),
            new DiffLine(DiffLineKind.Added, "b"),
            new DiffLine(DiffLineKind.Added, "c"),
        };

        var bands = DiffBackgroundRenderer.ComputeBands(diffLines);

        var band = Assert.Single(bands);
        Assert.Equal(new DiffBand(StartLine: 2, LineCount: 2, IsAddition: true), band);
    }

    [Fact]
    public void ComputeBands_PureDeletion_ReturnsOneZeroLengthMarkerAtTheBoundary()
    {
        var diffLines = new[]
        {
            new DiffLine(DiffLineKind.Unchanged, "a"),
            new DiffLine(DiffLineKind.Removed, "b"),
            new DiffLine(DiffLineKind.Removed, "c"),
            new DiffLine(DiffLineKind.Unchanged, "d"),
        };

        var bands = DiffBackgroundRenderer.ComputeBands(diffLines);

        var band = Assert.Single(bands);
        Assert.Equal(new DiffBand(StartLine: 2, LineCount: 0, IsAddition: false), band);
    }

    [Fact]
    public void ComputeBands_DeletionAtEndOfFile_MarkerAnchorsPastLastLine()
    {
        var diffLines = new[]
        {
            new DiffLine(DiffLineKind.Unchanged, "a"),
            new DiffLine(DiffLineKind.Removed, "b"),
        };

        var bands = DiffBackgroundRenderer.ComputeBands(diffLines);

        var band = Assert.Single(bands);
        Assert.Equal(new DiffBand(StartLine: 2, LineCount: 0, IsAddition: false), band);
    }

    [Fact]
    public void ComputeBands_ReplacementRun_ReturnsAddedBandAndOneDeletionMarker()
    {
        var diffLines = new[]
        {
            new DiffLine(DiffLineKind.Unchanged, "a"),
            new DiffLine(DiffLineKind.Removed, "old1"),
            new DiffLine(DiffLineKind.Removed, "old2"),
            new DiffLine(DiffLineKind.Added, "new1"),
            new DiffLine(DiffLineKind.Unchanged, "b"),
        };

        var bands = DiffBackgroundRenderer.ComputeBands(diffLines);

        Assert.Equal(2, bands.Count);
        Assert.Equal(new DiffBand(StartLine: 2, LineCount: 0, IsAddition: false), bands[0]);
        Assert.Equal(new DiffBand(StartLine: 2, LineCount: 1, IsAddition: true), bands[1]);
    }
}
