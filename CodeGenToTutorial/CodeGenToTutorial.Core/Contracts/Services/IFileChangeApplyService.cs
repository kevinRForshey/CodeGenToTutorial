using CodeGenToTutorial.Core.ViewModels;

namespace CodeGenToTutorial.Core.Contracts.Services;

public interface IFileChangeApplyService
{
    void Apply(ProposedFileChangeViewModel file, string workingDirectoryPath);

    // Writes raw content back to a path under workingDirectoryPath, preserving whatever
    // encoding/line endings are already on disk there (T4.1.3) - used by undo/revert to restore a
    // prior snapshot without a ProposedFileChangeViewModel to compose content from (T4.2.2).
    void WriteRaw(string filePath, string content, string workingDirectoryPath);
}
