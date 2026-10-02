using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

using CodeGenToTutorial.Core.Contracts.Services;
using CodeGenToTutorial.Core.Models;

namespace CodeGenToTutorial.Core.Services;

public class ClaudeCliService : IClaudeCliService
{
    private const string ExecutableName = "claude";

    public async Task<ClaudeCliResult> RunPromptAsync(string prompt, string? workingDirectory = null, CancellationToken cancellationToken = default)
    {
        // On Windows this is routed through cmd.exe so PATH resolution picks up the claude.cmd shim installed
        // by npm; other platforms can invoke the executable directly. The prompt is written to stdin rather
        // than passed as an argument: command-line parsers break on embedded newlines, which was
        // truncating/emptying multi-line prompts.
        var startInfo = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new ProcessStartInfo
            {
                FileName = "cmd.exe",
                ArgumentList = { "/c", ExecutableName, "-p" },
            }
            : new ProcessStartInfo
            {
                FileName = ExecutableName,
                ArgumentList = { "-p" },
            };

        startInfo.RedirectStandardInput = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.StandardInputEncoding = new UTF8Encoding(false);
        startInfo.StandardOutputEncoding = Encoding.UTF8;
        startInfo.StandardErrorEncoding = Encoding.UTF8;
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;

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
