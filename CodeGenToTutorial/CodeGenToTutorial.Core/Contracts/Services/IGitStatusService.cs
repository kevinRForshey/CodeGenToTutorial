using CodeGenToTutorial.Core.Models;

namespace CodeGenToTutorial.Core.Contracts.Services;

public interface IGitStatusService
{
    // Shells out to the git CLI to check whether workingDirectoryPath is inside a git working tree
    // and, if so, which files are dirty there (T4.3.1). Returns GitStatus.NotARepository - never
    // throws - if git isn't installed, the directory doesn't exist, or it isn't a repository; this
    // is an informational warning, not a security boundary, so a missing/failed git command should
    // never block an apply.
    GitStatus GetStatus(string workingDirectoryPath);
}
