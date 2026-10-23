using Paarde.Core.Models;

namespace Paarde.Core.Contracts.Services;

// Epic 16 (F16.1): the scoped-down "run a command, stream its output" terminal - same
// shell-out-and-capture shape as IBuildTestRunner (T12.1.1), generalized to an arbitrary
// user-typed command and, unlike IBuildTestRunner.RunAsync, reporting each line of output as it
// arrives (T16.1.3) rather than only once the process exits - ClaudeCliRunner.RunPromptAsync's
// onTextDelta callback is the same shape for the same reason.
public interface ITerminalCommandRunner
{
    Task<CommandResult> RunAsync(string command, string workingDirectoryPath, Action<string> onOutputLine, CancellationToken cancellationToken = default);
}
