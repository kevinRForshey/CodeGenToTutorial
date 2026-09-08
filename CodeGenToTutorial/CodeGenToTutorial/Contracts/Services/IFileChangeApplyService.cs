using CodeGenToTutorial.Models;

namespace CodeGenToTutorial.Contracts.Services;

public interface IFileChangeApplyService
{
    void Apply(ProposedFileChange file, string workingDirectoryPath);
}
