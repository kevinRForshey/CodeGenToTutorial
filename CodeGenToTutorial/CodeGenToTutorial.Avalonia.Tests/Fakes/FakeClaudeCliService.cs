using CodeGenToTutorial.Core.Contracts.Services;
using CodeGenToTutorial.Core.Models;

namespace CodeGenToTutorial.Avalonia.Tests.Fakes;

public class FakeClaudeCliService : IClaudeCliService
{
    public ClaudeCliResult ResultToReturn { get; set; } = new() { ExitCode = 0, StandardOutput = string.Empty };

    public Exception? ExceptionToThrow { get; set; }

    public string? LastPrompt { get; private set; }

    public string? LastWorkingDirectory { get; private set; }

    public int CallCount { get; private set; }

    public Task<ClaudeCliResult> RunPromptAsync(string prompt, string? workingDirectory = null, CancellationToken cancellationToken = default)
    {
        CallCount++;
        LastPrompt = prompt;
        LastWorkingDirectory = workingDirectory;

        if (ExceptionToThrow != null)
        {
            throw ExceptionToThrow;
        }

        return Task.FromResult(ResultToReturn);
    }
}
