using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

using CodeGenToTutorial.Core.Contracts.Services;
using CodeGenToTutorial.Core.Helpers;
using CodeGenToTutorial.Core.Models;

using Newtonsoft.Json.Linq;

namespace CodeGenToTutorial.Core.Services;

// The CLI implementation of ILlmRunner (T6.3.1) - a direct Anthropic API runner (T6.3.2) is meant
// to be a second one. CLI-specific configuration (executable path/arguments) is resolved through
// optionsProvider rather than taken as a per-call parameter, since Core can't depend on the
// Avalonia-layer settings service that actually stores it (see ClaudeCliOptionsProvider).
public class ClaudeCliRunner : ILlmRunner
{
    // Fixed flags required to get NDJSON stream-json output with token-level text deltas (T6.2.1) -
    // appended unconditionally after the user-configured arguments (T6.1.4), since streaming is now
    // a hard requirement for this service to parse its own output, not an optional CLI choice.
    private static readonly string[] StreamingArguments = { "--output-format", "stream-json", "--include-partial-messages", "--verbose" };

    private readonly Func<Task<ClaudeCliOptions>> _optionsProvider;

    public ClaudeCliRunner(Func<Task<ClaudeCliOptions>>? optionsProvider = null)
    {
        _optionsProvider = optionsProvider ?? (() => Task.FromResult(ClaudeCliOptions.Default));
    }

    public async Task<bool> IsAvailableAsync()
    {
        var options = await _optionsProvider();

        return ExecutablePathResolver.ExistsOnPath(options.ExecutablePath);
    }

    public async Task<LlmRunResult> RunPromptAsync(string prompt, string? workingDirectory = null, Action<string>? onTextDelta = null, CancellationToken cancellationToken = default)
    {
        var options = await _optionsProvider();
        var arguments = options.Arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries).Concat(StreamingArguments);

        // On Windows this is routed through cmd.exe so PATH resolution picks up the claude.cmd shim installed
        // by npm; other platforms can invoke the executable directly. The prompt is written to stdin rather
        // than passed as an argument: command-line parsers break on embedded newlines, which was
        // truncating/emptying multi-line prompts.
        var startInfo = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new ProcessStartInfo
            {
                FileName = "cmd.exe",
                ArgumentList = { "/c", options.ExecutablePath },
            }
            : new ProcessStartInfo
            {
                FileName = options.ExecutablePath,
            };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

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

        var assistantText = new StringBuilder();
        var standardError = new StringBuilder();
        double? totalCostUsd = null;
        long? durationMs = null;
        int? inputTokens = null;
        int? outputTokens = null;

        // Each line of stdout is one NDJSON object (T6.2.1) rather than plain text now that the CLI
        // is invoked with --output-format stream-json. A line that isn't a well-formed JSON object
        // is swallowed rather than crashing the run - the CLI shouldn't produce one, but this is an
        // external process's output, not something this code controls.
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null)
            {
                return;
            }

            JObject line;

            try
            {
                line = JObject.Parse(e.Data);
            }
            catch (Newtonsoft.Json.JsonException)
            {
                return;
            }

            switch (line["type"]?.ToString())
            {
                case "stream_event":
                    var streamEvent = line["event"] as JObject;

                    if (streamEvent?["type"]?.ToString() == "content_block_delta" &&
                        streamEvent["delta"] is JObject delta &&
                        delta["type"]?.ToString() == "text_delta")
                    {
                        var text = delta["text"]?.ToString() ?? string.Empty;
                        assistantText.Append(text);
                        onTextDelta?.Invoke(text);
                    }

                    break;

                case "result":
                    totalCostUsd = line["total_cost_usd"]?.ToObject<double?>();
                    durationMs = line["duration_ms"]?.ToObject<long?>();
                    inputTokens = (line["usage"] as JObject)?["input_tokens"]?.ToObject<int?>();
                    outputTokens = (line["usage"] as JObject)?["output_tokens"]?.ToObject<int?>();
                    break;
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

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // WaitForExitAsync throwing doesn't stop the child process on its own (T6.1.1) - without
            // this, cancelling (or a timeout firing) would leave `claude` - and, on Windows, the
            // cmd.exe wrapper's own child - running in the background after this call returns.
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            throw;
        }

        return new LlmRunResult
        {
            Success = process.ExitCode == 0,
            Output = assistantText.ToString().TrimEnd(),
            ErrorMessage = standardError.ToString().TrimEnd(),
            ExitCode = process.ExitCode,
            TotalCostUsd = totalCostUsd,
            DurationMs = durationMs,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
        };
    }
}
