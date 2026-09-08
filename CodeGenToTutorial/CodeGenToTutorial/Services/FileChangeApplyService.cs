using CodeGenToTutorial.Contracts.Services;
using CodeGenToTutorial.Models;

namespace CodeGenToTutorial.Services;

// Writes a proposed file's complete content to disk. A tutorial step's "Apply" button targets a single
// snippet, but the only content we can reliably write back is the whole file the CLI already produced,
// so applying any step for a file applies that file's full proposed change.
public class FileChangeApplyService : IFileChangeApplyService
{
    public void Apply(ProposedFileChange file, string workingDirectoryPath)
    {
        if (string.IsNullOrWhiteSpace(workingDirectoryPath))
        {
            throw new InvalidOperationException("A project location must be selected before applying changes.");
        }

        var fullPath = Path.Combine(workingDirectoryPath, file.FilePath);
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, file.Content);
    }
}
