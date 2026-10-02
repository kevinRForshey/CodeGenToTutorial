using CodeGenToTutorial.Core.Models;

namespace CodeGenToTutorial.Core.Contracts.Services;

public interface IClaudeCliService
{
    Task<ClaudeCliResult> RunPromptAsync(string prompt, string? workingDirectory = null, CancellationToken cancellationToken = default);
}
