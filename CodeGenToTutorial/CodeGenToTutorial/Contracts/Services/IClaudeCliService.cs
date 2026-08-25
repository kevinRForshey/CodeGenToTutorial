using CodeGenToTutorial.Models;

namespace CodeGenToTutorial.Contracts.Services;

public interface IClaudeCliService
{
    Task<ClaudeCliResult> RunPromptAsync(string prompt, string? workingDirectory = null, CancellationToken cancellationToken = default);
}
