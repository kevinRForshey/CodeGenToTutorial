using CodeGenToTutorial.Core.Contracts.Services;
using CodeGenToTutorial.Core.ViewModels;

namespace CodeGenToTutorial.Core.Services;

// Writes a proposed file's complete content to disk. A tutorial step's "Apply" button targets a single
// snippet, but the only content we can reliably write back is the whole file the CLI already produced,
// so applying any step for a file applies that file's full proposed change.
public class FileChangeApplyService : IFileChangeApplyService
{
    public void Apply(ProposedFileChangeViewModel file, string workingDirectoryPath)
    {
        if (string.IsNullOrWhiteSpace(workingDirectoryPath))
        {
            throw new InvalidOperationException("A project location must be selected before applying changes.");
        }

        var root = Path.GetFullPath(workingDirectoryPath);
        var fullPath = Path.GetFullPath(Path.Combine(root, file.FilePath));
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Refusing to write outside the project folder: '{file.FilePath}' resolves to '{fullPath}'.");
        }

        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, file.ProposedContent);
    }
}
