using CodeGenToTutorial.Core.Contracts.Services;
using CodeGenToTutorial.Core.Helpers;
using CodeGenToTutorial.Core.ViewModels;

namespace CodeGenToTutorial.Core.Services;

// Writes a proposed file's selected content to disk. The only content we can reliably write back is
// the whole file - there's no way to patch just one line range on a real filesystem write - but what
// that whole-file content *is* now depends on selection (F3.3): BuildSelectedContent composes it from
// EditedContent (the user's in-editor edits, or the LLM's ProposedContent untouched if the user hasn't
// edited) with any deselected hunks' changes backed out, or OriginalContent untouched if the file's
// whole-file fallback selection is off.
public class FileChangeApplyService : IFileChangeApplyService
{
    public void Apply(ProposedFileChangeViewModel file, string workingDirectoryPath)
    {
        var fullPath = FilePathContainment.Resolve(workingDirectoryPath, file.FilePath);
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, file.BuildSelectedContent());

        // T3.3.2: record that this write reflects the current selection, so each step's IsApplied
        // is accurate instead of the old MarkAllStepsApplied() workaround that marked every step
        // applied just because one of them triggered the write.
        file.MarkHunksAppliedFromSelection();
    }
}
