using System.Diagnostics;
using System.Text;

using CodeGenToTutorial.Contracts.Services;
using CodeGenToTutorial.Models;

namespace CodeGenToTutorial.Services;

public class ClaudeCliService : IClaudeCliService
{
    private const string ExecutableName = "claude";

    public async Task<ClaudeCliResult> RunPromptAsync(string prompt, string? workingDirectory = null, CancellationToken cancellationToken = default)
    {
        // Routed through cmd.exe so PATH resolution picks up the claude.cmd shim installed by npm on Windows.
        // The prompt is written to stdin rather than passed as an argument: cmd.exe's command-line parser
        // breaks on embedded newlines, which was truncating/emptying multi-line prompts.
        var startInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            ArgumentList = { "/c", ExecutableName, "-p" },
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        if (!string.IsNullOrWhiteSpace(workingDirectory) && Directory.Exists(workingDirectory))
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        using var process = new Process { StartInfo = startInfo };

        var standardOutput = new StringBuilder();
        var standardError = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                standardOutput.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                standardError.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.StandardInput.WriteAsync(prompt);
        process.StandardInput.Close();

        await process.WaitForExitAsync(cancellationToken);

        return new ClaudeCliResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = standardOutput.ToString().TrimEnd(),
            StandardError = standardError.ToString().TrimEnd(),
        };
    }
}
