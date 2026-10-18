using Paarde.Core.Contracts.Services;
using Paarde.Core.Models;
using Paarde.Core.ViewModels;

namespace Paarde.Core.Services;

// Writes one JSON file per run under sessionsFolderPath via IFileService - append-only across runs
// (never overwrites a previous run's file) so a later history page (T7.2.x) has every past run to
// list, not just the latest. Not thread-safe: like IPromptResultStore/IApplyHistoryLog, every
// caller today is the UI thread (PromptViewModel.RunPromptAsync, ApplyOrchestrationService).
public class RunSessionStore : IRunSessionStore
{
    private readonly IFileService _fileService;
    private readonly IPromptResultStore _promptResultStore;
    private readonly IApplyHistoryLog _applyHistoryLog;
    private readonly string _sessionsFolderPath;

    // Holds exactly the fields a session needs that aren't readable from IPromptResultStore/
    // IApplyHistoryLog at Resave time (those two are always read fresh, see Save below) - grouped
    // into one record rather than loose nullable fields so there's no "is everything set yet"
    // null-forgiving operator anywhere: _active is either the one complete session in progress, or
    // there isn't one yet.
    private sealed record ActiveSession(string Id, DateTimeOffset StartedAt, string FileName, string Prompt, string RawOutput);

    private ActiveSession? _active;

    public RunSessionStore(IFileService fileService, IPromptResultStore promptResultStore, IApplyHistoryLog applyHistoryLog, string sessionsFolderPath)
    {
        _fileService = fileService;
        _promptResultStore = promptResultStore;
        _applyHistoryLog = applyHistoryLog;
        _sessionsFolderPath = sessionsFolderPath;
    }

    public void RecordRun(string prompt, string rawOutput)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var id = Guid.NewGuid().ToString("N");

        // Sortable timestamp prefix so files list in run order without having to open and read
        // StartedAt from each one; the guid suffix only exists to rule out a same-millisecond clash.
        var fileName = $"{startedAt:yyyyMMddHHmmssfff}_{id}.json";

        _active = new ActiveSession(id, startedAt, fileName, prompt, rawOutput);
        Save(_active);
    }

    public void Resave()
    {
        if (_active is { } active)
        {
            Save(active);
        }
    }

    // Reads Files/Tutorial/WorkingDirectoryPath and the apply log fresh on every call (rather than
    // caching them on RecordRun) so Resave always reflects whatever selections/applies have
    // happened since - the whole point of calling it again instead of leaving the RecordRun
    // snapshot in place.
    private void Save(ActiveSession active)
    {
        var session = new RunSession(
            active.Id,
            active.StartedAt,
            _promptResultStore.WorkingDirectoryPath,
            active.Prompt,
            active.RawOutput,
            _promptResultStore.Tutorial,
            _promptResultStore.Files.Select(ToRunSessionFile).ToList(),
            _applyHistoryLog.Entries.ToList());

        _fileService.Save(_sessionsFolderPath, active.FileName, session);
    }

    private static RunSessionFile ToRunSessionFile(ProposedFileChangeViewModel file) => new(
        file.FilePath,
        file.OriginalContent,
        file.ProposedContent,
        file.EditedContent,
        file.HasSelectableHunks ? true : file.IsSelected ?? true,
        file.Hunks.Select(ToRunSessionHunk).ToList());

    private static RunSessionHunk ToRunSessionHunk(ProposedHunk hunk) =>
        new(hunk.Title, hunk.Explanation, hunk.Snippet, hunk.Location, hunk.IsSelected);
}
