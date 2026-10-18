using System.Runtime.Versioning;

using Paarde.Core.Models;
using Paarde.Core.Services;

namespace Paarde.Core.Tests.Services;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]

// Stands a POSIX shell script in for the `claude` CLI (same approach GitStatusServiceTests takes with a
// real `git` binary) so RunPromptAsync's stream-json NDJSON parsing (T6.2.1/T6.2.3) can be exercised
// without invoking the real, paid Claude Code CLI.
public class ClaudeCliRunnerTests : IDisposable
{
    private const string FakeClaudeScript = """
        #!/bin/sh
        cat > /dev/null
        cat <<'EOF'
        {"type":"system","subtype":"init"}
        {"type":"stream_event","event":{"type":"content_block_delta","delta":{"type":"text_delta","text":"Hello"}}}
        {"type":"stream_event","event":{"type":"content_block_delta","delta":{"type":"text_delta","text":", world"}}}
        {"type":"stream_event","event":{"type":"content_block_stop"}}
        {"type":"result","subtype":"success","is_error":false,"duration_ms":1234,"total_cost_usd":0.0056,"usage":{"input_tokens":10,"output_tokens":5}}
        EOF
        """;

    private readonly string _scriptPath = Path.Combine(Path.GetTempPath(), $"fake-claude-{Guid.NewGuid()}.sh");
    private readonly ClaudeCliRunner _runner;

    public ClaudeCliRunnerTests()
    {
        File.WriteAllText(_scriptPath, FakeClaudeScript);
        File.SetUnixFileMode(_scriptPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        var options = new ClaudeCliOptions(_scriptPath, string.Empty);
        _runner = new ClaudeCliRunner(() => Task.FromResult(options));
    }

    public void Dispose()
    {
        if (File.Exists(_scriptPath))
        {
            File.Delete(_scriptPath);
        }
    }

    [Fact]
    public async Task RunPromptAsync_StreamJsonTextDeltas_AccumulatesThemAsOutput()
    {
        var result = await _runner.RunPromptAsync("prompt");

        Assert.Equal("Hello, world", result.Output);
        Assert.True(result.Success);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task RunPromptAsync_StreamJsonTextDeltas_InvokesOnTextDeltaForEachChunkInOrder()
    {
        var received = new List<string>();

        await _runner.RunPromptAsync("prompt", onTextDelta: received.Add);

        Assert.Equal(new[] { "Hello", ", world" }, received);
    }

    [Fact]
    public async Task RunPromptAsync_ResultLine_PopulatesCostAndUsageFields()
    {
        var result = await _runner.RunPromptAsync("prompt");

        Assert.Equal(0.0056, result.TotalCostUsd);
        Assert.Equal(1234, result.DurationMs);
        Assert.Equal(10, result.InputTokens);
        Assert.Equal(5, result.OutputTokens);
    }

    [Fact]
    public async Task IsAvailableAsync_ScriptExists_ReturnsTrue()
    {
        Assert.True(await _runner.IsAvailableAsync());
    }

    [Fact]
    public async Task IsAvailableAsync_ExecutableMissing_ReturnsFalse()
    {
        var runner = new ClaudeCliRunner(() => Task.FromResult(new ClaudeCliOptions("/no/such/claude-binary", string.Empty)));

        Assert.False(await runner.IsAvailableAsync());
    }
}
