namespace CodeGenToTutorial.Core.Models;

// Arguments is split on whitespace when building the process's argument list (T6.1.4) - there's no
// quoting support, so an argument value that itself needs to contain a space isn't representable.
public record ClaudeCliOptions(string ExecutablePath, string Arguments)
{
    public static readonly ClaudeCliOptions Default = new("claude", "-p");
}
