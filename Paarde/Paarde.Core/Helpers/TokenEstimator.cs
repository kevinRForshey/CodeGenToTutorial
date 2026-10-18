namespace Paarde.Core.Helpers;

// Anthropic doesn't expose a local tokenizer, and the point of this estimate is to update live as
// the user types/attaches files - not to round-trip to an API endpoint on every keystroke. ~4
// characters per token is the same rough ratio Anthropic's own docs use for English text; it's a
// ballpark for display, not the precise count the CLI reports from the real API response after a
// run (LlmRunResult.InputTokens).
public static class TokenEstimator
{
    private const double CharactersPerToken = 4.0;

    public static int EstimateTokenCount(string text) =>
        string.IsNullOrEmpty(text) ? 0 : (int)Math.Ceiling(text.Length / CharactersPerToken);
}
