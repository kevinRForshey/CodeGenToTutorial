using CodeGenToTutorial.Core.Models;

namespace CodeGenToTutorial.Core.Contracts.Services;

public interface IClaudeCliService
{
    // options defaults to ClaudeCliOptions.Default ("claude -p") when null; passing a non-null
    // value is how T6.1.4's configurable CLI path/arguments setting reaches the actual invocation.
    // onTextDelta (T6.2.1), if given, is invoked with each chunk of assistant text as the CLI
    // streams it - on a background thread, so callers that touch UI state must marshal themselves.
    Task<ClaudeCliResult> RunPromptAsync(string prompt, string? workingDirectory = null, ClaudeCliOptions? options = null, Action<string>? onTextDelta = null, CancellationToken cancellationToken = default);

    // Cheap preflight (T6.1.3): lets callers report a missing CLI with actionable install guidance
    // instead of surfacing whatever exception Process.Start happens to throw.
    bool IsAvailable(ClaudeCliOptions? options = null);
}
