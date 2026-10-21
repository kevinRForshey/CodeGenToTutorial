using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;

using Paarde.Core.Contracts.Services;
using Paarde.Core.Models;

using Newtonsoft.Json.Linq;

namespace Paarde.Core.Services;

// The direct-API implementation of ILlmRunner (T6.3.2) - a second implementation alongside
// ClaudeCliRunner, behind the same interface per T6.3.1's design. Unlike the CLI, the raw Messages
// API is stateless (no --resume equivalent): this runner owns conversation history itself, keyed by
// the SessionId it hands back, so resumeSessionId can still work the same way callers already rely
// on for ClaudeCliRunner. That history only lives in memory for this runner instance's lifetime
// (the app's lifetime, since it's registered as a singleton) - it doesn't persist across restarts
// the way the CLI's own session storage does.
public class AnthropicApiRunner : ILlmRunner
{
    private const string MessagesEndpoint = "https://api.anthropic.com/v1/messages";
    private const string AnthropicVersion = "2023-06-01";
    private const int MaxTokens = 8192;

    private static readonly HttpClient SharedHttpClient = new();

    private readonly Func<Task<AnthropicApiOptions>> _optionsProvider;
    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, List<JObject>> _conversations = new();

    public AnthropicApiRunner(Func<Task<AnthropicApiOptions>>? optionsProvider = null, HttpClient? httpClient = null)
    {
        _optionsProvider = optionsProvider ?? (() => Task.FromResult(AnthropicApiOptions.Default));
        _httpClient = httpClient ?? SharedHttpClient;
    }

    public async Task<bool> IsAvailableAsync()
    {
        var options = await _optionsProvider();

        return !string.IsNullOrWhiteSpace(options.ApiKey);
    }

    public async Task<LlmRunResult> RunPromptAsync(string prompt, string? workingDirectory = null, Action<string>? onTextDelta = null, string? resumeSessionId = null, CancellationToken cancellationToken = default)
    {
        var options = await _optionsProvider();

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return new LlmRunResult { Success = false, ErrorMessage = "No Anthropic API key configured. Add one on the Settings page." };
        }

        // T7.3.1 parity: continues the same back-and-forth resumeSessionId identifies (if any history
        // is still held for it) by sending the whole prior transcript back, since the API itself has
        // no server-side session concept to resume like the CLI does.
        var history = resumeSessionId is not null && _conversations.TryGetValue(resumeSessionId, out var existing)
            ? new List<JObject>(existing)
            : new List<JObject>();

        history.Add(new JObject { ["role"] = "user", ["content"] = prompt });

        var requestBody = new JObject
        {
            ["model"] = options.Model,
            ["max_tokens"] = MaxTokens,
            ["stream"] = true,
            ["messages"] = new JArray(history),
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, MessagesEndpoint)
        {
            Content = new StringContent(requestBody.ToString(), Encoding.UTF8, "application/json"),
        };

        request.Headers.Add("x-api-key", options.ApiKey);
        request.Headers.Add("anthropic-version", AnthropicVersion);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        var stopwatch = Stopwatch.StartNew();

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            return new LlmRunResult { Success = false, ErrorMessage = ExtractErrorMessage(errorBody, response.StatusCode.ToString()) };
        }

        var assistantText = new StringBuilder();
        int? inputTokens = null;
        int? outputTokens = null;
        string? stopReason = null;

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (!line.StartsWith("data: ", StringComparison.Ordinal))
            {
                continue;
            }

            JObject payload;

            try
            {
                payload = JObject.Parse(line["data: ".Length..]);
            }
            catch (Newtonsoft.Json.JsonException)
            {
                continue;
            }

            switch (payload["type"]?.ToString())
            {
                case "message_start":
                    inputTokens = (payload["message"]?["usage"] as JObject)?["input_tokens"]?.ToObject<int?>();
                    break;

                case "content_block_delta":
                    if (payload["delta"] is JObject { } delta && delta["type"]?.ToString() == "text_delta")
                    {
                        var text = delta["text"]?.ToString() ?? string.Empty;
                        assistantText.Append(text);
                        onTextDelta?.Invoke(text);
                    }

                    break;

                case "message_delta":
                    outputTokens = (payload["usage"] as JObject)?["output_tokens"]?.ToObject<int?>() ?? outputTokens;
                    stopReason = payload["delta"]?["stop_reason"]?.ToString() ?? stopReason;
                    break;
            }
        }

        stopwatch.Stop();

        var output = assistantText.ToString();

        // refusal/other isn't modeled as a failure here - the same way the CLI runner only fails on a
        // non-zero exit code, not on the content of what came back - but max_tokens getting hit before
        // the model finished is worth surfacing distinctly, since the caller's JSON parse is about to
        // fail on truncated output and "the API call itself failed" would be the wrong explanation.
        if (stopReason == "max_tokens")
        {
            return new LlmRunResult { Success = false, Output = output, ErrorMessage = $"The response was cut off at the {MaxTokens}-token limit before finishing." };
        }

        history.Add(new JObject { ["role"] = "assistant", ["content"] = output });
        var sessionId = resumeSessionId is not null && _conversations.ContainsKey(resumeSessionId) ? resumeSessionId : Guid.NewGuid().ToString();
        _conversations[sessionId] = history;

        return new LlmRunResult
        {
            Success = true,
            Output = output,
            DurationMs = stopwatch.ElapsedMilliseconds,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            SessionId = sessionId,

            // Unlike the CLI's "result" line, the Messages API doesn't return a total_cost_usd - doing
            // that here would mean hand-maintaining a per-model price table that silently goes stale
            // whenever Anthropic's pricing changes, so this is left null rather than guessed at.
            TotalCostUsd = null,
        };
    }

    private static string ExtractErrorMessage(string responseBody, string fallback)
    {
        try
        {
            var error = JObject.Parse(responseBody);
            return error["error"]?["message"]?.ToString() ?? fallback;
        }
        catch (Newtonsoft.Json.JsonException)
        {
            return fallback;
        }
    }
}
