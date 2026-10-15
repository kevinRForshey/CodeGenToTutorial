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

    public CancellationToken LastCancellationToken { get; private set; }

    // When false, RunPromptAsync blocks until cancellationToken is cancelled instead of returning
    // immediately - stands in for a real `claude` invocation that's still running, so tests can
    // exercise PromptViewModel.CancelPromptCommand without a real process.
    public bool CompleteImmediately { get; set; } = true;

    public bool AvailableToReturn { get; set; } = true;

    public ClaudeCliOptions? LastOptions { get; private set; }

    // T6.2.1: text chunks RunPromptAsync should hand to onTextDelta, in order, before returning.
    public IReadOnlyList<string> TextDeltasToEmit { get; set; } = Array.Empty<string>();

    public bool IsAvailable(ClaudeCliOptions? options = null) => AvailableToReturn;

    public async Task<ClaudeCliResult> RunPromptAsync(string prompt, string? workingDirectory = null, ClaudeCliOptions? options = null, Action<string>? onTextDelta = null, CancellationToken cancellationToken = default)
    {
        CallCount++;
        LastPrompt = prompt;
        LastWorkingDirectory = workingDirectory;
        LastOptions = options;
        LastCancellationToken = cancellationToken;

        if (ExceptionToThrow != null)
        {
            throw ExceptionToThrow;
        }

        if (!CompleteImmediately)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        foreach (var delta in TextDeltasToEmit)
        {
            onTextDelta?.Invoke(delta);
        }

        return ResultToReturn;
    }
}
