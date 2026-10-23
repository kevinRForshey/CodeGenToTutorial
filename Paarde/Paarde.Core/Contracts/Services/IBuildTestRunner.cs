using Paarde.Core.Models;

namespace Paarde.Core.Contracts.Services;

// Runs an arbitrary project command (the Settings-configured build/test command, T12.1.2) and
// captures its output (T12.1.1 - Epic 12, the automated build/test feedback loop). Deliberately
// provider-agnostic about *what* command runs - a build command, a test command, and (Epic 16's
// integrated terminal) any user-typed command all go through the same contract.
public interface IBuildTestRunner
{
    // command is split on whitespace the same way ClaudeCliOptions.Arguments already is - no
    // quoting support, so an argument that itself needs an embedded space isn't representable.
    // Never throws for an ordinary command failure (non-zero exit, stderr output) - that's a
    // normal result, not an exceptional one; only a cancellation/timeout propagates as an
    // OperationCanceledException, matching ClaudeCliRunner.RunPromptAsync's contract.
    Task<CommandResult> RunAsync(string command, string workingDirectoryPath, CancellationToken cancellationToken = default);
}
