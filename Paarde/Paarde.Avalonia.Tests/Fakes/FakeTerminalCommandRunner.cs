using Paarde.Core.Contracts.Services;
using Paarde.Core.Models;

namespace Paarde.Avalonia.Tests.Fakes;

public class FakeTerminalCommandRunner : ITerminalCommandRunner
{
    public string? LastCommand { get; private set; }

    public string? LastWorkingDirectoryPath { get; private set; }

    public CommandResult ResultToReturn { get; set; } = new(0, string.Empty, string.Empty, TimeSpan.Zero);

    // Lines reported to the caller's onOutputLine callback before RunAsync returns - stands in for
    // ProcessCommandRunner's real line-by-line streaming (T16.1.3).
    public IReadOnlyList<string> LinesToStream { get; set; } = Array.Empty<string>();

    public bool ThrowOperationCanceledOnNextRun { get; set; }

    public Task<CommandResult> RunAsync(string command, string workingDirectoryPath, Action<string> onOutputLine, CancellationToken cancellationToken = default)
    {
        LastCommand = command;
        LastWorkingDirectoryPath = workingDirectoryPath;

        if (ThrowOperationCanceledOnNextRun)
        {
            ThrowOperationCanceledOnNextRun = false;
            throw new OperationCanceledException();
        }

        foreach (var line in LinesToStream)
        {
            onOutputLine(line);
        }

        return Task.FromResult(ResultToReturn);
    }
}
