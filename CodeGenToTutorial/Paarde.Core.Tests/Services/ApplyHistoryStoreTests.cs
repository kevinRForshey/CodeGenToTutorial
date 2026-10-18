using Paarde.Core.Models;
using Paarde.Core.Services;

namespace Paarde.Core.Tests.Services;

public class ApplyHistoryStoreTests
{
    private readonly ApplyHistoryStore _store = new();

    [Fact]
    public void HasHistory_NothingRecorded_IsFalse()
    {
        Assert.False(_store.HasHistory);
    }

    [Fact]
    public void TryPopLastBatch_NothingRecorded_ReturnsFalseWithEmptyChanges()
    {
        var found = _store.TryPopLastBatch(out var changes);

        Assert.False(found);
        Assert.Empty(changes);
    }

    [Fact]
    public void RecordBatch_EmptyChanges_DoesNotAddToHistory()
    {
        _store.RecordBatch(Array.Empty<AppliedFileChange>());

        Assert.False(_store.HasHistory);
    }

    [Fact]
    public void RecordBatch_NonEmptyChanges_TryPopLastBatchReturnsThem()
    {
        var changes = new[] { new AppliedFileChange("a.txt", "original") };
        _store.RecordBatch(changes);

        var found = _store.TryPopLastBatch(out var popped);

        Assert.True(found);
        Assert.Equal(changes, popped);
    }

    [Fact]
    public void TryPopLastBatch_TwoBatchesRecorded_PopsMostRecentFirst()
    {
        _store.RecordBatch(new[] { new AppliedFileChange("a.txt", "first batch content") });
        _store.RecordBatch(new[] { new AppliedFileChange("a.txt", "second batch content") });

        _store.TryPopLastBatch(out var firstPop);

        Assert.Equal("second batch content", Assert.Single(firstPop).ContentBeforeApply);
    }

    [Fact]
    public void TryPopLastBatch_AfterPoppingTheOnlyBatch_HasHistoryIsFalse()
    {
        _store.RecordBatch(new[] { new AppliedFileChange("a.txt", "original") });
        _store.TryPopLastBatch(out _);

        Assert.False(_store.HasHistory);
    }

    [Fact]
    public void Clear_WithRecordedBatches_RemovesThemAll()
    {
        _store.RecordBatch(new[] { new AppliedFileChange("a.txt", "original") });

        _store.Clear();

        Assert.False(_store.HasHistory);
        Assert.False(_store.TryPopLastBatch(out _));
    }
}
