namespace CodeGenToTutorial.Core.Models;

public class ClaudeCliResult
{
    public int ExitCode { get; init; }

    public string StandardOutput { get; init; } = string.Empty;

    public string StandardError { get; init; } = string.Empty;

    // Populated from the final "result" line of the CLI's stream-json output (T6.2.3) - null if that
    // line was never seen (e.g. the process was cancelled or killed before completing).
    public double? TotalCostUsd { get; init; }

    public long? DurationMs { get; init; }

    public int? InputTokens { get; init; }

    public int? OutputTokens { get; init; }
}
