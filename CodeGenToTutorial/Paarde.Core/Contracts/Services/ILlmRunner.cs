using Paarde.Core.Models;

namespace Paarde.Core.Contracts.Services;

// Provider-agnostic abstraction over "send a prompt to an LLM and get a response" (T6.3.1).
// ClaudeCliRunner is the only implementation today; a direct Anthropic API runner (T6.3.2) is
// meant to be a second one behind the same interface, which is why nothing CLI-specific (an
// executable path, process arguments) appears here - each implementation owns resolving its own
// configuration.
public interface ILlmRunner
{
    // onTextDelta, if given, is invoked with each chunk of assistant text as the response streams
    // in - on a background thread for ClaudeCliRunner, so callers that touch UI state must marshal
    // themselves.
    Task<LlmRunResult> RunPromptAsync(string prompt, string? workingDirectory = null, Action<string>? onTextDelta = null, CancellationToken cancellationToken = default);

    // Cheap preflight: lets callers report an unavailable runner with actionable guidance instead
    // of surfacing whatever exception the underlying call happens to throw.
    Task<bool> IsAvailableAsync();
}
