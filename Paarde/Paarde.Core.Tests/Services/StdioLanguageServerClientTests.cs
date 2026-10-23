using System.Runtime.Versioning;

using Paarde.Core.Models;
using Paarde.Core.Services;

namespace Paarde.Core.Tests.Services;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]

// Stands a Python script in for a real LSP server (same "fake external process" approach
// ClaudeCliRunnerTests takes with a shell script) so the Content-Length framing, request/response
// correlation, and diagnostics-push handling (T17.1.1/T17.1.2) can be exercised without depending
// on csharp-ls (or any other real language server) being installed - per the roadmap's explicit
// instruction not to test against a real one.
public class StdioLanguageServerClientTests : IAsyncLifetime
{
    private const string FakeLspServerScript = """
        #!/usr/bin/env python3
        import sys, json

        def read_message():
            content_length = None
            while True:
                line = sys.stdin.buffer.readline()
                if line in (b"", b"\r\n", b"\n"):
                    break
                if line.lower().startswith(b"content-length:"):
                    content_length = int(line.split(b":")[1].strip())
            if content_length is None:
                return None
            return json.loads(sys.stdin.buffer.read(content_length))

        def write_message(obj):
            body = json.dumps(obj).encode("utf-8")
            sys.stdout.buffer.write(f"Content-Length: {len(body)}\r\n\r\n".encode("ascii") + body)
            sys.stdout.buffer.flush()

        while True:
            message = read_message()
            if message is None:
                break

            method = message.get("method")
            msg_id = message.get("id")

            if method == "initialize":
                write_message({"jsonrpc": "2.0", "id": msg_id, "result": {"capabilities": {}}})
            elif method == "textDocument/didOpen":
                write_message({
                    "jsonrpc": "2.0",
                    "method": "textDocument/publishDiagnostics",
                    "params": {
                        "uri": message["params"]["textDocument"]["uri"],
                        "diagnostics": [{
                            "range": {"start": {"line": 0, "character": 0}, "end": {"line": 0, "character": 5}},
                            "message": "fake diagnostic",
                            "severity": 1,
                        }],
                    },
                })
            elif method == "textDocument/definition":
                write_message({
                    "jsonrpc": "2.0",
                    "id": msg_id,
                    "result": {
                        "uri": "file:///tmp/definition-target.cs",
                        "range": {"start": {"line": 4, "character": 2}, "end": {"line": 4, "character": 10}},
                    },
                })
            elif method == "textDocument/references":
                write_message({
                    "jsonrpc": "2.0",
                    "id": msg_id,
                    "result": [
                        {"uri": "file:///tmp/ref-one.cs", "range": {"start": {"line": 1, "character": 0}, "end": {"line": 1, "character": 3}}},
                        {"uri": "file:///tmp/ref-two.cs", "range": {"start": {"line": 2, "character": 0}, "end": {"line": 2, "character": 3}}},
                    ],
                })
            elif method == "shutdown":
                write_message({"jsonrpc": "2.0", "id": msg_id, "result": None})
            elif method == "exit":
                break
        """;

    private readonly string _scriptPath = Path.Combine(Path.GetTempPath(), $"fake-lsp-server-{Guid.NewGuid()}.py");
    private readonly StdioLanguageServerClient _client;

    public StdioLanguageServerClientTests()
    {
        File.WriteAllText(_scriptPath, FakeLspServerScript);
        File.SetUnixFileMode(_scriptPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        var options = new LspOptions(_scriptPath, string.Empty);
        _client = new StdioLanguageServerClient(() => Task.FromResult(options));
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _client.StopAsync();

        if (File.Exists(_scriptPath))
        {
            File.Delete(_scriptPath);
        }
    }

    [Fact]
    public async Task IsAvailableAsync_ScriptExists_ReturnsTrue()
    {
        Assert.True(await _client.IsAvailableAsync());
    }

    [Fact]
    public async Task IsAvailableAsync_ExecutableMissing_ReturnsFalse()
    {
        var client = new StdioLanguageServerClient(() => Task.FromResult(new LspOptions("/no/such/lsp-binary", string.Empty)));

        Assert.False(await client.IsAvailableAsync());
    }

    [Fact]
    public async Task StartAsync_ValidServer_SetsIsRunningTrue()
    {
        await _client.StartAsync(Path.GetTempPath());

        Assert.True(_client.IsRunning);
    }

    [Fact]
    public async Task StopAsync_RunningServer_SetsIsRunningFalse()
    {
        await _client.StartAsync(Path.GetTempPath());

        await _client.StopAsync();

        Assert.False(_client.IsRunning);
    }

    [Fact]
    public async Task GetDefinitionAsync_ServerRespondsWithASingleLocation_ReturnsThatLocation()
    {
        await _client.StartAsync(Path.GetTempPath());

        var locations = await _client.GetDefinitionAsync(Path.Combine(Path.GetTempPath(), "source.cs"), new LspPosition(0, 0));

        var location = Assert.Single(locations);
        Assert.Equal("/tmp/definition-target.cs", location.FilePath);
        Assert.Equal(4, location.Position.Line);
        Assert.Equal(2, location.Position.Character);
    }

    [Fact]
    public async Task GetReferencesAsync_ServerRespondsWithAnArray_ReturnsEveryLocationInOrder()
    {
        await _client.StartAsync(Path.GetTempPath());

        var locations = await _client.GetReferencesAsync(Path.Combine(Path.GetTempPath(), "source.cs"), new LspPosition(0, 0));

        Assert.Equal(2, locations.Count);
        Assert.Equal("/tmp/ref-one.cs", locations[0].FilePath);
        Assert.Equal("/tmp/ref-two.cs", locations[1].FilePath);
    }

    [Fact]
    public async Task NotifyDocumentOpenedAsync_ServerPublishesDiagnostics_RaisesDiagnosticsPublishedForThatFile()
    {
        var filePath = Path.Combine(Path.GetTempPath(), "fake-lsp-doc.cs");
        var tcs = new TaskCompletionSource<(string FilePath, IReadOnlyList<LspDiagnostic> Diagnostics)>(TaskCreationOptions.RunContinuationsAsynchronously);
        _client.DiagnosticsPublished += (path, diagnostics) => tcs.TrySetResult((path, diagnostics));

        await _client.StartAsync(Path.GetTempPath());
        await _client.NotifyDocumentOpenedAsync(filePath, "class Foo {}");

        var (publishedPath, diagnostics) = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(Path.GetFullPath(filePath), publishedPath);
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("fake diagnostic", diagnostic.Message);
        Assert.Equal(LspDiagnosticSeverity.Error, diagnostic.Severity);
    }
}
