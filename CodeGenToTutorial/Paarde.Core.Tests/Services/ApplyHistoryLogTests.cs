using Paarde.Core.Models;
using Paarde.Core.Services;

namespace Paarde.Core.Tests.Services;

public class ApplyHistoryLogTests
{
    private readonly ApplyHistoryLog _log = new();

    [Fact]
    public void Entries_NothingRecorded_IsEmpty()
    {
        Assert.Empty(_log.Entries);
    }

    [Fact]
    public void Record_EmptyFilePaths_DoesNotAddAnEntry()
    {
        _log.Record(ApplyHistoryEntryKind.Applied, Array.Empty<string>());

        Assert.Empty(_log.Entries);
    }

    [Fact]
    public void Record_NonEmptyFilePaths_AddsAnEntryWithKindAndFiles()
    {
        _log.Record(ApplyHistoryEntryKind.Applied, new[] { "a.txt", "b.txt" });

        var entry = Assert.Single(_log.Entries);
        Assert.Equal(ApplyHistoryEntryKind.Applied, entry.Kind);
        Assert.Equal(new[] { "a.txt", "b.txt" }, entry.FilePaths);
        Assert.Equal(2, entry.FileCount);
    }

    [Fact]
    public void Record_MultipleCalls_PreservesInsertionOrder()
    {
        _log.Record(ApplyHistoryEntryKind.Applied, new[] { "a.txt" });
        _log.Record(ApplyHistoryEntryKind.UndoLastApply, new[] { "a.txt" });
        _log.Record(ApplyHistoryEntryKind.RevertAllApplied, new[] { "a.txt", "b.txt" });

        Assert.Equal(
            new[] { ApplyHistoryEntryKind.Applied, ApplyHistoryEntryKind.UndoLastApply, ApplyHistoryEntryKind.RevertAllApplied },
            _log.Entries.Select(e => e.Kind));
    }
}
