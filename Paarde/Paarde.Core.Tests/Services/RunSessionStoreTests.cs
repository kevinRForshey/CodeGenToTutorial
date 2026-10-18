using Paarde.Core.Models;
using Paarde.Core.Services;
using Paarde.Core.ViewModels;

namespace Paarde.Core.Tests.Services;

public class RunSessionStoreTests : IDisposable
{
    private readonly string _sessionsFolder = Directory.CreateTempSubdirectory("PaardeTests_Sessions_").FullName;
    private readonly PromptResultStore _promptResultStore = new();
    private readonly ApplyHistoryLog _applyHistoryLog = new();
    private readonly RunSessionStore _store;

    public RunSessionStoreTests()
    {
        _store = new RunSessionStore(new FileService(), _promptResultStore, _applyHistoryLog, _sessionsFolder);
    }

    public void Dispose()
    {
        if (Directory.Exists(_sessionsFolder))
        {
            Directory.Delete(_sessionsFolder, recursive: true);
        }
    }

    private string[] SessionFiles() => Directory.Exists(_sessionsFolder) ? Directory.GetFiles(_sessionsFolder) : Array.Empty<string>();

    private RunSession ReadOnlySessionFile() => new FileService().Read<RunSession>(_sessionsFolder, Path.GetFileName(Assert.Single(SessionFiles())))!;

    [Fact]
    public void RecordRun_WritesOneFileToTheSessionsFolder()
    {
        _promptResultStore.SetResult("tutorial text", Array.Empty<ProposedFileChangeViewModel>(), "/working/dir");

        _store.RecordRun("do the thing", "raw cli output", "user prompt");

        Assert.Single(SessionFiles());
    }

    [Fact]
    public void RecordRun_PersistsPromptRawOutputTutorialAndWorkingDirectory()
    {
        _promptResultStore.SetResult("tutorial text", Array.Empty<ProposedFileChangeViewModel>(), "/working/dir");

        _store.RecordRun("do the thing", "raw cli output", "user prompt");
        var session = ReadOnlySessionFile();

        Assert.Equal("do the thing", session.Prompt);
        Assert.Equal("raw cli output", session.RawOutput);
        Assert.Equal("tutorial text", session.Tutorial);
        Assert.Equal("/working/dir", session.WorkingDirectoryPath);
    }

    [Fact]
    public void RecordRun_PersistsEachFileWithItsContentAndHunkSelections()
    {
        var file = new ProposedFileChangeViewModel("a.cs", "proposed", "original")
        {
            Hunks = new[] { new ProposedHunk("Add Foo", "Explains Foo", "class Foo {}", new HunkLocation(0, 2)) { IsSelected = false } },
        };
        _promptResultStore.SetResult("tutorial text", new[] { file }, "/working/dir");

        _store.RecordRun("prompt", "raw output", "user prompt");
        var session = ReadOnlySessionFile();

        var persistedFile = Assert.Single(session.Files);
        Assert.Equal("a.cs", persistedFile.FilePath);
        Assert.Equal("original", persistedFile.OriginalContent);
        Assert.Equal("proposed", persistedFile.ProposedContent);
        Assert.Equal("proposed", persistedFile.EditedContent);
        var persistedHunk = Assert.Single(persistedFile.Hunks);
        Assert.Equal("Add Foo", persistedHunk.Title);
        Assert.False(persistedHunk.IsSelected);
    }

    [Fact]
    public void RecordRun_FileWithNoHunks_PersistsTheWholeFileFallbackSelection()
    {
        var file = new ProposedFileChangeViewModel("a.cs", "proposed", "original") { IsSelected = false };
        _promptResultStore.SetResult("tutorial text", new[] { file }, "/working/dir");

        _store.RecordRun("prompt", "raw output", "user prompt");
        var session = ReadOnlySessionFile();

        var persistedFile = Assert.Single(session.Files);
        Assert.Empty(persistedFile.Hunks);
        Assert.False(persistedFile.WholeFileSelected);
    }

    [Fact]
    public void RecordRun_PersistsTheCurrentApplyLog()
    {
        _applyHistoryLog.Record(ApplyHistoryEntryKind.Applied, new[] { "a.cs" });

        _store.RecordRun("prompt", "raw output", "user prompt");
        var session = ReadOnlySessionFile();

        var entry = Assert.Single(session.ApplyLog);
        Assert.Equal(ApplyHistoryEntryKind.Applied, entry.Kind);
        Assert.Equal(new[] { "a.cs" }, entry.FilePaths);
    }

    [Fact]
    public void Resave_BeforeAnyRecordRun_DoesNotWriteAFile()
    {
        _store.Resave();

        Assert.Empty(SessionFiles());
    }

    [Fact]
    public void Resave_AfterRecordRun_OverwritesTheSameFileRatherThanCreatingANewOne()
    {
        _store.RecordRun("prompt", "raw output", "user prompt");
        Assert.Single(SessionFiles());

        _store.Resave();

        Assert.Single(SessionFiles());
    }

    [Fact]
    public void Resave_PicksUpSelectionChangesMadeAfterRecordRun()
    {
        var file = new ProposedFileChangeViewModel("a.cs", "proposed", "original") { IsSelected = true };
        _promptResultStore.SetResult("tutorial text", new[] { file }, "/working/dir");
        _store.RecordRun("prompt", "raw output", "user prompt");

        file.IsSelected = false;
        _store.Resave();

        var session = ReadOnlySessionFile();
        Assert.False(Assert.Single(session.Files).WholeFileSelected);
    }

    [Fact]
    public void Resave_PicksUpApplyLogEntriesRecordedAfterRecordRun()
    {
        _store.RecordRun("prompt", "raw output", "user prompt");
        Assert.Empty(ReadOnlySessionFile().ApplyLog);

        _applyHistoryLog.Record(ApplyHistoryEntryKind.Applied, new[] { "a.cs" });
        _store.Resave();

        Assert.Single(ReadOnlySessionFile().ApplyLog);
    }

    [Fact]
    public void RecordRun_CalledTwice_KeepsThePreviousSessionFileAndStartsANewOne()
    {
        _store.RecordRun("first prompt", "first output", "first user prompt");
        _store.RecordRun("second prompt", "second output", "second user prompt");

        Assert.Equal(2, SessionFiles().Length);
    }

    [Fact]
    public void RestoreLastSession_NoSessionsFolder_ReturnsNullAndTouchesNeitherStore()
    {
        var restored = _store.RestoreLastSession();

        Assert.Null(restored);
        Assert.Empty(_promptResultStore.Files);
        Assert.Empty(_applyHistoryLog.Entries);
    }

    [Fact]
    public void RestoreLastSession_NoSessionsRecordedButFolderExists_ReturnsNull()
    {
        Directory.CreateDirectory(_sessionsFolder);

        Assert.Null(_store.RestoreLastSession());
    }

    [Fact]
    public void RestoreLastSession_PicksTheMostRecentlyRecordedSession()
    {
        _promptResultStore.SetResult("first tutorial", Array.Empty<ProposedFileChangeViewModel>(), "/first");
        _store.RecordRun("first prompt", "first output", "first user prompt");

        _promptResultStore.SetResult("second tutorial", Array.Empty<ProposedFileChangeViewModel>(), "/second");
        _store.RecordRun("second prompt", "second output", "second user prompt");

        // Fresh store/instances, as if the app had just relaunched.
        var freshPromptResultStore = new PromptResultStore();
        var freshApplyHistoryLog = new ApplyHistoryLog();
        var freshStore = new RunSessionStore(new FileService(), freshPromptResultStore, freshApplyHistoryLog, _sessionsFolder);

        var restored = freshStore.RestoreLastSession();

        Assert.Equal("second prompt", restored!.Prompt);
        Assert.Equal("second tutorial", freshPromptResultStore.Tutorial);
        Assert.Equal("/second", freshPromptResultStore.WorkingDirectoryPath);
    }

    [Fact]
    public void RestoreLastSession_RestoresFileContentAndHunkSelection()
    {
        var file = new ProposedFileChangeViewModel("a.cs", "proposed", "original")
        {
            EditedContent = "edited",
            Hunks = new[] { new ProposedHunk("Add Foo", "Explains Foo", "class Foo {}", new HunkLocation(0, 2)) { IsSelected = false } },
        };
        _promptResultStore.SetResult("tutorial text", new[] { file }, "/working/dir");
        _store.RecordRun("prompt", "raw output", "user prompt");

        var freshPromptResultStore = new PromptResultStore();
        var freshStore = new RunSessionStore(new FileService(), freshPromptResultStore, new ApplyHistoryLog(), _sessionsFolder);
        freshStore.RestoreLastSession();

        var restoredFile = Assert.Single(freshPromptResultStore.Files);
        Assert.Equal("a.cs", restoredFile.FilePath);
        Assert.Equal("original", restoredFile.OriginalContent);
        Assert.Equal("proposed", restoredFile.ProposedContent);
        Assert.Equal("edited", restoredFile.EditedContent);
        var restoredHunk = Assert.Single(restoredFile.Hunks);
        Assert.Equal("Add Foo", restoredHunk.Title);
        Assert.False(restoredHunk.IsSelected);
    }

    [Fact]
    public void RestoreLastSession_RestoresWholeFileFallbackSelectionWhenThereAreNoHunks()
    {
        var file = new ProposedFileChangeViewModel("a.cs", "proposed", "original") { IsSelected = false };
        _promptResultStore.SetResult("tutorial text", new[] { file }, "/working/dir");
        _store.RecordRun("prompt", "raw output", "user prompt");

        var freshPromptResultStore = new PromptResultStore();
        var freshStore = new RunSessionStore(new FileService(), freshPromptResultStore, new ApplyHistoryLog(), _sessionsFolder);
        freshStore.RestoreLastSession();

        var restoredFile = Assert.Single(freshPromptResultStore.Files);
        Assert.Empty(restoredFile.Hunks);
        Assert.False(restoredFile.IsSelected);
    }

    [Fact]
    public void RestoreLastSession_RestoresTheApplyLog()
    {
        _applyHistoryLog.Record(ApplyHistoryEntryKind.Applied, new[] { "a.cs" });
        _store.RecordRun("prompt", "raw output", "user prompt");

        var freshApplyHistoryLog = new ApplyHistoryLog();
        var freshStore = new RunSessionStore(new FileService(), new PromptResultStore(), freshApplyHistoryLog, _sessionsFolder);
        freshStore.RestoreLastSession();

        var entry = Assert.Single(freshApplyHistoryLog.Entries);
        Assert.Equal(ApplyHistoryEntryKind.Applied, entry.Kind);
        Assert.Equal(new[] { "a.cs" }, entry.FilePaths);
    }

    [Fact]
    public void RestoreLastSession_ThenResave_OverwritesTheRestoredFileRatherThanCreatingANewOne()
    {
        _store.RecordRun("prompt", "raw output", "user prompt");
        Assert.Single(SessionFiles());

        var freshStore = new RunSessionStore(new FileService(), new PromptResultStore(), new ApplyHistoryLog(), _sessionsFolder);
        freshStore.RestoreLastSession();
        freshStore.Resave();

        Assert.Single(SessionFiles());
    }

    [Fact]
    public void RecordRun_PersistsUserPromptTextSeparatelyFromTheComposedPrompt()
    {
        _store.RecordRun("instructions + user prompt + working dir", "raw output", "user prompt");

        var session = ReadOnlySessionFile();

        Assert.Equal("instructions + user prompt + working dir", session.Prompt);
        Assert.Equal("user prompt", session.UserPromptText);
    }

    [Fact]
    public void RestoreLastSession_RestoresUserPromptTextOntoTheReadoptedActiveSession()
    {
        _store.RecordRun("composed prompt", "raw output", "user prompt");

        var freshStore = new RunSessionStore(new FileService(), new PromptResultStore(), new ApplyHistoryLog(), _sessionsFolder);
        var restored = freshStore.RestoreLastSession();

        Assert.Equal("user prompt", restored!.UserPromptText);
    }

    [Fact]
    public void ListSessions_NoSessionsFolder_ReturnsEmpty()
    {
        Assert.Empty(_store.ListSessions());
    }

    [Fact]
    public void ListSessions_ReturnsEveryRecordedSessionNewestFirst()
    {
        _store.RecordRun("first prompt", "first output", "first user prompt");
        _store.RecordRun("second prompt", "second output", "second user prompt");

        var sessions = _store.ListSessions();

        Assert.Equal(new[] { "second prompt", "first prompt" }, sessions.Select(s => s.Prompt));
    }

    [Fact]
    public void ListSessions_DoesNotTouchPromptResultStoreOrApplyHistoryLog()
    {
        _store.RecordRun("prompt", "raw output", "user prompt");

        _store.ListSessions();

        Assert.Empty(_promptResultStore.Files);
        Assert.Empty(_applyHistoryLog.Entries);
    }
}
