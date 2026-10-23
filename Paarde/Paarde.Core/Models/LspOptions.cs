namespace Paarde.Core.Models;

// Arguments is split on whitespace when building the process's argument list (same limitation as
// ClaudeCliOptions.Arguments) - good enough for csharp-ls, which takes none by default.
public record LspOptions(string ExecutablePath, string Arguments)
{
    public static readonly LspOptions Default = new("csharp-ls", string.Empty);
}
