using System.Net;
using System.Text;

using Paarde.Core.Models;
using Paarde.Core.Services;

using Newtonsoft.Json.Linq;

namespace Paarde.Core.Tests.Services;

// Stands a fake HttpMessageHandler in for the real api.anthropic.com endpoint (same approach
// ClaudeCliRunnerTests takes with a fake CLI script) so streaming SSE parsing can be exercised
// without a real, paid API call.
public class AnthropicApiRunnerTests
{
    private const string StreamedResponse = """
        data: {"type":"message_start","message":{"usage":{"input_tokens":10}}}

        data: {"type":"content_block_delta","delta":{"type":"text_delta","text":"Hello"}}

        data: {"type":"content_block_delta","delta":{"type":"text_delta","text":", world"}}

        data: {"type":"message_delta","delta":{"stop_reason":"end_turn"},"usage":{"output_tokens":5}}

        data: {"type":"message_stop"}

        """;

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        public string? LastRequestBody { get; private set; }

        public HttpStatusCode StatusCodeToReturn { get; set; } = HttpStatusCode.OK;

        public string ResponseBodyToReturn { get; set; } = StreamedResponse;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(StatusCodeToReturn)
            {
                Content = new StringContent(ResponseBodyToReturn, Encoding.UTF8, "text/event-stream"),
            };
        }
    }

    private readonly FakeHttpMessageHandler _handler = new();
    private readonly AnthropicApiOptions _options = new("test-api-key", "claude-sonnet-5");
    private AnthropicApiRunner CreateRunner(AnthropicApiOptions? options = null) =>
        new(() => Task.FromResult(options ?? _options), new HttpClient(_handler));

    [Fact]
    public async Task RunPromptAsync_StreamedTextDeltas_AccumulatesThemAsOutput()
    {
        var runner = CreateRunner();

        var result = await runner.RunPromptAsync("prompt");

        Assert.True(result.Success);
        Assert.Equal("Hello, world", result.Output);
    }

    [Fact]
    public async Task RunPromptAsync_StreamedTextDeltas_InvokesOnTextDeltaForEachChunkInOrder()
    {
        var runner = CreateRunner();
        var received = new List<string>();

        await runner.RunPromptAsync("prompt", onTextDelta: received.Add);

        Assert.Equal(new[] { "Hello", ", world" }, received);
    }

    [Fact]
    public async Task RunPromptAsync_MessageDeltaLine_PopulatesUsageFields()
    {
        var runner = CreateRunner();

        var result = await runner.RunPromptAsync("prompt");

        Assert.Equal(10, result.InputTokens);
        Assert.Equal(5, result.OutputTokens);
        Assert.Null(result.TotalCostUsd);
        Assert.Null(result.ExitCode);
        Assert.NotNull(result.SessionId);
    }

    [Fact]
    public async Task RunPromptAsync_SendsModelAndApiKeyFromOptions()
    {
        var runner = CreateRunner();

        await runner.RunPromptAsync("prompt");

        Assert.Equal("test-api-key", _handler.LastRequest!.Headers.GetValues("x-api-key").Single());
        var body = JObject.Parse(_handler.LastRequestBody!);
        Assert.Equal("claude-sonnet-5", body["model"]!.ToString());
    }

    [Fact]
    public async Task RunPromptAsync_NoApiKeyConfigured_ReturnsFailureWithoutMakingARequest()
    {
        var runner = CreateRunner(new AnthropicApiOptions(string.Empty, "claude-sonnet-5"));

        var result = await runner.RunPromptAsync("prompt");

        Assert.False(result.Success);
        Assert.Null(_handler.LastRequest);
    }

    [Fact]
    public async Task RunPromptAsync_NonSuccessStatusCode_ReturnsFailureWithTheApisErrorMessage()
    {
        _handler.StatusCodeToReturn = HttpStatusCode.Unauthorized;
        _handler.ResponseBodyToReturn = """{"type":"error","error":{"type":"authentication_error","message":"invalid x-api-key"}}""";
        var runner = CreateRunner();

        var result = await runner.RunPromptAsync("prompt");

        Assert.False(result.Success);
        Assert.Equal("invalid x-api-key", result.ErrorMessage);
    }

    [Fact]
    public async Task RunPromptAsync_MaxTokensStopReason_ReturnsFailureButKeepsThePartialOutput()
    {
        _handler.ResponseBodyToReturn = """
            data: {"type":"content_block_delta","delta":{"type":"text_delta","text":"Truncated"}}

            data: {"type":"message_delta","delta":{"stop_reason":"max_tokens"},"usage":{"output_tokens":8192}}

            """;
        var runner = CreateRunner();

        var result = await runner.RunPromptAsync("prompt");

        Assert.False(result.Success);
        Assert.Equal("Truncated", result.Output);
    }

    [Fact]
    public async Task RunPromptAsync_NoResumeSessionIdGiven_SendsOnlyTheNewUserMessage()
    {
        var runner = CreateRunner();

        await runner.RunPromptAsync("first prompt");

        var messages = (JArray)JObject.Parse(_handler.LastRequestBody!)["messages"]!;
        Assert.Single(messages);
        Assert.Equal("first prompt", messages[0]["content"]!.ToString());
    }

    [Fact]
    public async Task RunPromptAsync_ResumeSessionIdGiven_IncludesPriorTurnsInTheRequest()
    {
        var runner = CreateRunner();

        var first = await runner.RunPromptAsync("first prompt");
        await runner.RunPromptAsync("second prompt", resumeSessionId: first.SessionId);

        var messages = (JArray)JObject.Parse(_handler.LastRequestBody!)["messages"]!;
        Assert.Equal(3, messages.Count);
        Assert.Equal("first prompt", messages[0]["content"]!.ToString());
        Assert.Equal("user", messages[0]["role"]!.ToString());
        Assert.Equal("assistant", messages[1]["role"]!.ToString());
        Assert.Equal("second prompt", messages[2]["content"]!.ToString());
    }

    [Fact]
    public async Task RunPromptAsync_UnknownResumeSessionId_StartsFreshHistoryAndReturnsANewSessionId()
    {
        var runner = CreateRunner();

        var result = await runner.RunPromptAsync("prompt", resumeSessionId: "never-seen-before");

        var messages = (JArray)JObject.Parse(_handler.LastRequestBody!)["messages"]!;
        Assert.Single(messages);
        Assert.NotEqual("never-seen-before", result.SessionId);
    }

    [Fact]
    public async Task IsAvailableAsync_ApiKeyConfigured_ReturnsTrue()
    {
        var runner = CreateRunner();

        Assert.True(await runner.IsAvailableAsync());
    }

    [Fact]
    public async Task IsAvailableAsync_ApiKeyBlank_ReturnsFalse()
    {
        var runner = CreateRunner(new AnthropicApiOptions(string.Empty, "claude-sonnet-5"));

        Assert.False(await runner.IsAvailableAsync());
    }
}
