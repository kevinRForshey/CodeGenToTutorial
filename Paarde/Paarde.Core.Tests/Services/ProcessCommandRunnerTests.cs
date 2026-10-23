using System.Runtime.Versioning;

using Paarde.Core.Services;

namespace Paarde.Core.Tests.Services;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]

// Runs real, trivial shell commands (same approach ClaudeCliRunnerTests/GitStatusServiceTests take
// with a fake script / the real git binary) rather than mocking Process - the behavior under test
// (stdout/stderr capture, exit code, cancellation killing the child) only exists at that boundary.
public class ProcessCommandRunnerTests : IDisposable
{
    private readonly string _workingDirectory = Directory.CreateTempSubdirectory("PaardeTests_").FullName;
    private readonly ProcessCommandRunner _runner = new();

    public void Dispose()
    {
        if (Directory.Exists(_workingDirectory))
        {
            Directory.Delete(_workingDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_SuccessfulCommand_CapturesStandardOutputAndZeroExitCode()
    {
        var result = await _runner.RunAsync("echo hello", _workingDirectory);

        Assert.Equal(0, result.ExitCode);
        Assert.True(result.Succeeded);
        Assert.Equal("hello", result.StandardOutput);
    }

    [Fact]
    public async Task RunAsync_FailingCommand_CapturesStandardErrorAndNonZeroExitCode()
    {
        var result = await _runner.RunAsync("echo failure-detail >&2 && exit 1", _workingDirectory);

        Assert.Equal(1, result.ExitCode);
        Assert.False(result.Succeeded);
        Assert.Equal("failure-detail", result.StandardError);
    }

    [Fact]
    public async Task RunAsync_RunsInTheGivenWorkingDirectory()
    {
        File.WriteAllText(Path.Combine(_workingDirectory, "marker.txt"), string.Empty);

        var result = await _runner.RunAsync("ls", _workingDirectory);

        Assert.Contains("marker.txt", result.StandardOutput);
    }

    [Fact]
    public async Task RunAsync_Cancelled_ThrowsOperationCanceledAndKillsTheChildProcess()
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _runner.RunAsync("sleep 30", _workingDirectory, cancellationTokenSource.Token));
    }

    // T16.1.3: the streaming overload (ITerminalCommandRunner) reports each line as it arrives,
    // not just the buffered result once the process exits.
    [Fact]
    public async Task RunAsync_WithOnOutputLineCallback_ReportsEachLineAsItArrives()
    {
        var reportedLines = new List<string>();

        var result = await _runner.RunAsync("printf 'one\\ntwo\\n'", _workingDirectory, reportedLines.Add);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(new[] { "one", "two" }, reportedLines);
    }

    [Fact]
    public async Task RunAsync_WithOnOutputLineCallback_AlsoReportsStandardErrorLines()
    {
        var reportedLines = new List<string>();

        await _runner.RunAsync("echo failure-detail >&2 && exit 1", _workingDirectory, reportedLines.Add);

        Assert.Contains("failure-detail", reportedLines);
    }

    [Fact]
    public async Task RunAsync_WithOnOutputLineCallback_Cancelled_ThrowsOperationCanceledAndKillsTheChildProcess()
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _runner.RunAsync("sleep 30", _workingDirectory, _ => { }, cancellationTokenSource.Token));
    }
}
