using CodeGenToTutorial.Core.ViewModels;

namespace CodeGenToTutorial.Core.Contracts.Services;

public interface IFileChangeApplyService
{
    void Apply(ProposedFileChangeViewModel file, string workingDirectoryPath);
}
