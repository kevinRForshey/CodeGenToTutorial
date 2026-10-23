using Paarde.Core.Models;

namespace Paarde.Core.Contracts.Services;

// Provider-agnostic LSP client (T17.1.1) - mirrors ILlmRunner's shape (an IsAvailableAsync
// preflight, a single implementation today) but one implementation is a long-lived process tied to
// a working directory rather than a one-shot-per-call process, so it also owns explicit
// start/stop lifecycle (T17.2.1) and a diagnostics push subscription instead of a single
// request/response per call.
public interface ILanguageServerClient
{
    bool IsRunning { get; }

    // DiagnosticsPublished(filePath, diagnostics) fires on a background thread (the server's own
    // notification stream), same marshaling caveat as ILlmRunner's onTextDelta - filePath is an
    // absolute path, already converted from the server's file:// URI.
    event Action<string, IReadOnlyList<LspDiagnostic>>? DiagnosticsPublished;

    Task<bool> IsAvailableAsync();

    // Starts (or, if already running against a different working directory, restarts) the server
    // process rooted at workingDirectoryPath. A no-op if already running against the same path.
    Task StartAsync(string workingDirectoryPath, CancellationToken cancellationToken = default);

    Task StopAsync();

    Task NotifyDocumentOpenedAsync(string filePath, string text);

    // Full-document sync (T17.2.1's simplest correct option) - text is the entire current content,
    // not an incremental edit.
    Task NotifyDocumentChangedAsync(string filePath, string text);

    Task NotifyDocumentClosedAsync(string filePath);

    Task<IReadOnlyList<LspLocation>> GetDefinitionAsync(string filePath, LspPosition position, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LspLocation>> GetReferencesAsync(string filePath, LspPosition position, CancellationToken cancellationToken = default);
}
