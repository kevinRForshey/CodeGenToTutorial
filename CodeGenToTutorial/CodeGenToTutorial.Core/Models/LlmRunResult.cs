namespace CodeGenToTutorial.Core.Models;

// Generic result of running a prompt through any ILlmRunner implementation (T6.3.1). ExitCode is
// nullable because it's a CLI-process concept - a future direct-API runner (T6.3.2) has no
// equivalent and can leave it null; callers should branch on Success, not ExitCode.
public class LlmRunResult
{
    public bool Success { get; init; }

    public string Output { get; init; } = string.Empty;

    public string ErrorMessage { get; init; } = string.Empty;

    public int? ExitCode { get; init; }

    // Populated from the final "result" line of the CLI's stream-json output (T6.2.3) - null if that
    // line was never seen (e.g. the process was cancelled or killed before completing), or if the
    // runner implementation doesn't report usage.
    public double? TotalCostUsd { get; init; }

    public long? DurationMs { get; init; }

    public int? InputTokens { get; init; }

    public int? OutputTokens { get; init; }
}
