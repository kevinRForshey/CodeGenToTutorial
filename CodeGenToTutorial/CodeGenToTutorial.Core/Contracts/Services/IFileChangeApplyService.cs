using CodeGenToTutorial.Core.Models;

namespace CodeGenToTutorial.Core.Contracts.Services;

public interface IFileChangeApplyService
{
    void Apply(ProposedFileChange file, string workingDirectoryPath);
}
