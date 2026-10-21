namespace Paarde.Core.Models;

// Which ILlmRunner implementation (T6.3.1) a run should go through - read by Avalonia's
// LlmRunnerSelector (the registered ILlmRunner) to decide which concrete runner to delegate to.
public enum LlmRunnerKind
{
    ClaudeCli,
    AnthropicApi,
}
