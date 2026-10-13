using CodeGenToTutorial.Core.Services;

namespace CodeGenToTutorial.Core.Tests.Services;

public class ApplyBackupStoreTests
{
    private readonly ApplyBackupStore _store = new();

    [Fact]
    public void TryGetSnapshot_NoSnapshotForPath_ReturnsFalse()
    {
        var found = _store.TryGetSnapshot("a.txt", out var content);

        Assert.False(found);
        Assert.Null(content);
    }

    [Fact]
    public void SnapshotIfAbsent_FirstCallForPath_StoresContent()
    {
        _store.SnapshotIfAbsent("a.txt", "original content");

        var found = _store.TryGetSnapshot("a.txt", out var content);

        Assert.True(found);
        Assert.Equal("original content", content);
    }

    [Fact]
    public void SnapshotIfAbsent_SecondCallForSamePath_KeepsTheFirstContent()
    {
        _store.SnapshotIfAbsent("a.txt", "original content");
        _store.SnapshotIfAbsent("a.txt", "intermediate content after a second apply");

        _store.TryGetSnapshot("a.txt", out var content);

        Assert.Equal("original content", content);
    }

    [Fact]
    public void SnapshotIfAbsent_DifferentPaths_AreTrackedIndependently()
    {
        _store.SnapshotIfAbsent("a.txt", "a content");
        _store.SnapshotIfAbsent("b.txt", "b content");

        _store.TryGetSnapshot("a.txt", out var aContent);
        _store.TryGetSnapshot("b.txt", out var bContent);

        Assert.Equal("a content", aContent);
        Assert.Equal("b content", bContent);
    }

    [Fact]
    public void AllSnapshots_NoSnapshots_IsEmpty()
    {
        Assert.Empty(_store.AllSnapshots);
    }

    [Fact]
    public void AllSnapshots_AfterTrackingTwoPaths_ContainsBoth()
    {
        _store.SnapshotIfAbsent("a.txt", "a content");
        _store.SnapshotIfAbsent("b.txt", "b content");

        Assert.Equal(2, _store.AllSnapshots.Count);
        Assert.Equal("a content", _store.AllSnapshots["a.txt"]);
        Assert.Equal("b content", _store.AllSnapshots["b.txt"]);
    }
}
