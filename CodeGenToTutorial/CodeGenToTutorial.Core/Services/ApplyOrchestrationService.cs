using CodeGenToTutorial.Core.Contracts.Services;
using CodeGenToTutorial.Core.Helpers;
using CodeGenToTutorial.Core.Models;
using CodeGenToTutorial.Core.ViewModels;

namespace CodeGenToTutorial.Core.Services;

// Drives "Apply Selected" across every file in a tutorial result (F3.3), on top of
// IFileChangeApplyService's single-file write. Each file is handled independently: a conflict or a
// failed write on one file is recorded and the batch moves on to the next rather than stopping
// (T3.3.4).
public class ApplyOrchestrationService : IApplyOrchestrationService
{
    private readonly IFileChangeApplyService _fileChangeApplyService;

    public ApplyOrchestrationService(IFileChangeApplyService fileChangeApplyService)
    {
        _fileChangeApplyService = fileChangeApplyService;
    }

    public ApplyPreview Preview(IReadOnlyList<ProposedFileChangeViewModel> files, string workingDirectoryPath)
    {
        var filesToApply = files.Where(f => f.HasAnySelectedChange).ToList();

        // A whole-file fallback (no resolved hunks) counts as one change; otherwise count each
        // selected step individually, matching what the per-file SelectionSummary already shows.
        var changeCount = filesToApply.Sum(f => f.HasSelectableHunks ? f.SelectedHunkCount : 1);

        // Surfaced up front (T4.1.2) so the confirmation prompt can warn about protected targets
        // before ApplySelected skips them, rather than the user only finding out after the fact.
        var protectedPathFiles = filesToApply
            .Where(f => ProtectedPathChecker.IsProtected(workingDirectoryPath, f.FilePath))
            .Select(f => f.FilePath)
            .ToList();

        return new ApplyPreview(filesToApply.Count, changeCount)
        {
            ProtectedPathCount = protectedPathFiles.Count,
            // Array.Empty<string>() rather than the (possibly empty) List<string> above: record
            // equality falls back to reference equality for a non-IEquatable property like
            // IReadOnlyList<string>, and Array.Empty<string>() is a cached singleton, so this keeps
            // "no protected files" comparing equal to ApplyPreview.ProtectedPathFiles's own default
            // instead of failing Assert.Equal purely because one side is a List and the other an array.
            ProtectedPathFiles = protectedPathFiles.Count == 0 ? Array.Empty<string>() : protectedPathFiles,
        };
    }

    public ApplyBatchResult ApplySelected(IReadOnlyList<ProposedFileChangeViewModel> files, string workingDirectoryPath, bool allowProtectedPaths = false)
    {
        var results = new List<FileApplyResult>();

        foreach (var file in files.Where(f => f.HasAnySelectedChange))
        {
            results.Add(ApplyOne(file, workingDirectoryPath, allowProtectedPaths));
        }

        return new ApplyBatchResult(results);
    }

    private FileApplyResult ApplyOne(ProposedFileChangeViewModel file, string workingDirectoryPath, bool allowProtectedPaths)
    {
        // Checked before touching disk at all (T4.1.2) - unlike Conflict, there's nothing to re-diff
        // and retry here, so this short-circuits ahead of the read/write below rather than sharing
        // their try/catch.
        if (!allowProtectedPaths && ProtectedPathChecker.IsProtected(workingDirectoryPath, file.FilePath))
        {
            return new FileApplyResult(file.FilePath, FileApplyOutcome.ProtectedPathBlocked);
        }

        string fullPath;
        string currentDiskContent;

        try
        {
            // Read what's actually on disk right now, not file.OriginalContent (the baseline this
            // proposal was diffed against) - if the file changed since then (someone edited it
            // outside the app, or a previous apply already wrote part of it), writing over it would
            // silently discard that. FilePathContainment.Resolve enforces the same "stay inside the
            // project folder" rule the write itself will, so this read can't be used to probe
            // outside it either.
            fullPath = FilePathContainment.Resolve(workingDirectoryPath, file.FilePath);
            currentDiskContent = FileContentReader.ReadOrEmpty(fullPath);
        }
        catch (InvalidOperationException ex)
        {
            // The path-containment check (or a blank working directory) rejected it - treat the same
            // as any other failed write rather than letting it escape the batch or the read.
            return new FileApplyResult(file.FilePath, FileApplyOutcome.Failed, ex.Message);
        }

        if (currentDiskContent != file.OriginalContent)
        {
            // Don't write - the caller can offer file.RefreshOriginalContent(workingDirectoryPath) to
            // re-diff against what's actually there and let the user decide again (T3.3.3).
            return new FileApplyResult(file.FilePath, FileApplyOutcome.Conflict);
        }

        try
        {
            // Delegates the actual write to IFileChangeApplyService rather than writing
            // BuildSelectedContent() to fullPath directly here, so there's exactly one place that
            // knows how selection turns into bytes on disk and marks each hunk's IsApplied
            // afterward (T3.3.2) - this orchestration only decides which files get attempted and
            // reports what happened to each.
            _fileChangeApplyService.Apply(file, workingDirectoryPath);
            return new FileApplyResult(file.FilePath, FileApplyOutcome.Applied);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return new FileApplyResult(file.FilePath, FileApplyOutcome.Failed, ex.Message);
        }
    }
}
