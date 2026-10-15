using CodeGenToTutorial.Core.Models;

namespace CodeGenToTutorial.Core.Contracts.Services;

public interface IClaudeCliService
{
    Task<ClaudeCliResult> RunPromptAsync(string prompt, string? workingDirectory = null, CancellationToken cancellationToken = default);

    // Cheap preflight (T6.1.3): lets callers report a missing CLI with actionable install guidance
    // instead of surfacing whatever exception Process.Start happens to throw.
    bool IsAvailable();
}
