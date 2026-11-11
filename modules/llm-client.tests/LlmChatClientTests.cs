using System.Net;
using System.Text.Json;
using Aura.LlmClient;

namespace Aura.LlmClient.Tests;

public sealed class LlmChatClientTests
{
    private static readonly Uri BaseUrl = new("https://llm.example.com/v1/");

    [Fact]
    public async Task NonStreamingRequestMapsParametersAndReturnsCompletion()
    {
        var responseJson = """
            {
              "id": "chatcmpl-123",
              "object": "chat.completion",
              "created": 1700000000,
              "model": "server-model",
              "choices": [
                {
                  "index": 0,
                  "message": { "role": "assistant", "content": "It is sunny." },
                  "finish_reason": "stop"
                }
              ],
              "usage": {
                "prompt_tokens": 10,
                "completion_tokens": 4,
                "total_tokens": 14
              }
            }
            """;
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(StubHttpMessageHandler.JsonResponse(responseJson)));
        var client = CreateClient(
            handler,
            new Dictionary<string, string> { ["X-Provider"] = "test-provider" });
        var request = new ChatRequest
        {
            Messages =
            [
                new ChatMessage { Role = ChatRole.System, Content = "Be concise." },
                new ChatMessage { Role = ChatRole.User, Content = "Weather in Paris?" },
                new ChatMessage
                {
                    Role = ChatRole.Assistant,
                    ToolCalls =
                    [
                        new ChatToolCall
                        {
                            Id = "call_1",
                            Name = "get_weather",
                            Arguments = """{"city":"Paris"}""",
                        },
                    ],
                },
                new ChatMessage
                {
                    Role = ChatRole.Tool,
                    ToolCallId = "call_1",
                    Content = "sunny",
                },
            ],
            Model = "request-model",
            Stream = false,
            Temperature = 0.5f,
            TopP = 0.8f,
            MaxOutputTokens = 128,
            StopSequences = ["END"],
            Tools =
            [
                new ChatToolDefinition
                {
                    Name = "get_weather",
                    Description = "Gets the weather.",
                    ParametersJsonSchema = """
                        {"type":"object","properties":{"city":{"type":"string"}},"required":["city"]}
                        """,
                    Strict = true,
                },
            ],
            ToolChoice = new ChatToolChoice { Mode = ChatToolChoiceMode.Auto },
            AllowParallelToolCalls = false,
            ResponseFormat = ChatResponseFormat.JsonObject(),
        };

        var events = await CollectAsync(client.ChatAsync(request));

        var completed = Assert.IsType<ChatCompletedEvent>(Assert.Single(events));
        Assert.Equal("chatcmpl-123", completed.ResponseId);
        Assert.Equal("server-model", completed.Model);
        Assert.Equal("It is sunny.", completed.Text);
        Assert.Empty(completed.ToolCalls);
        Assert.Equal("Stop", completed.FinishReason);
        Assert.Equal(new ChatUsage
        {
            InputTokens = 10,
            OutputTokens = 4,
            TotalTokens = 14,
        }, completed.Usage);

        var captured = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, captured.Method);
        Assert.Equal("https://llm.example.com/v1/chat/completions", captured.Uri.AbsoluteUri);
        Assert.True(captured.Headers.ContainsKey("Authorization"));
        Assert.Equal("Bearer test-key", captured.Headers["Authorization"]);
        Assert.Equal("test-provider", captured.Headers["X-Provider"]);

        using var body = JsonDocument.Parse(captured.Body);
        var root = body.RootElement;
        Assert.Equal("request-model", root.GetProperty("model").GetString());
        Assert.False(
            root.TryGetProperty("stream", out var stream) && stream.GetBoolean());
        Assert.Equal(0.5, root.GetProperty("temperature").GetDouble(), 3);
        Assert.Equal(0.8, root.GetProperty("top_p").GetDouble(), 3);
        Assert.Equal(128, root.GetProperty("max_completion_tokens").GetInt32());
        Assert.Equal("END", root.GetProperty("stop")[0].GetString());
        Assert.Equal("json_object", root.GetProperty("response_format").GetProperty("type").GetString());
        Assert.False(root.GetProperty("parallel_tool_calls").GetBoolean());

        var messages = root.GetProperty("messages");
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal("user", messages[1].GetProperty("role").GetString());
        Assert.Equal("assistant", messages[2].GetProperty("role").GetString());
        Assert.Equal(
            "call_1",
            messages[2].GetProperty("tool_calls")[0].GetProperty("id").GetString());
        Assert.Equal("tool", messages[3].GetProperty("role").GetString());
        Assert.Equal("call_1", messages[3].GetProperty("tool_call_id").GetString());
        Assert.Equal(
            "get_weather",
            root.GetProperty("tools")[0].GetProperty("function").GetProperty("name").GetString());
    }

    [Fact]
    public async Task StreamingRequestEmitsTextDeltasAndCompletion()
    {
        var events = """
            data: {"id":"chatcmpl-stream","object":"chat.completion.chunk","created":1700000000,"model":"stream-model","choices":[{"index":0,"delta":{"role":"assistant","content":"Hel"},"finish_reason":null}]}

            data: {"id":"chatcmpl-stream","object":"chat.completion.chunk","created":1700000000,"model":"stream-model","choices":[{"index":0,"delta":{"content":"lo"},"finish_reason":"stop"}]}

            data: {"id":"chatcmpl-stream","object":"chat.completion.chunk","created":1700000000,"model":"stream-model","choices":[],"usage":{"prompt_tokens":3,"completion_tokens":2,"total_tokens":5}}

            data: [DONE]

            """;
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(StubHttpMessageHandler.EventStreamResponse(events)));
        var client = CreateClient(handler);

        var collected = await CollectAsync(
            client.ChatAsync(SimpleRequest(stream: true, model: null)));

        Assert.Collection(
            collected,
            item => Assert.Equal("Hel", Assert.IsType<TextDeltaEvent>(item).Text),
            item => Assert.Equal("lo", Assert.IsType<TextDeltaEvent>(item).Text),
            item =>
            {
                var completed = Assert.IsType<ChatCompletedEvent>(item);
                Assert.Null(completed.ResponseId);
                Assert.Equal("default-model", completed.Model);
                Assert.Equal("Hello", completed.Text);
                Assert.Equal("Stop", completed.FinishReason);
                Assert.Equal(5, completed.Usage?.TotalTokens);
            });

        using var body = JsonDocument.Parse(Assert.Single(handler.Requests).Body);
        Assert.True(body.RootElement.GetProperty("stream").GetBoolean());
        Assert.Equal("default-model", body.RootElement.GetProperty("model").GetString());
        Assert.True(
            body.RootElement.TryGetProperty("stream_options", out var streamOptions),
            Assert.Single(handler.Requests).Body);
        Assert.True(streamOptions.GetProperty("include_usage").GetBoolean());
    }

    [Fact]
    public async Task StreamingToolCallFragmentsAreAggregated()
    {
        var events = """
            data: {"id":"chatcmpl-tool","object":"chat.completion.chunk","created":1700000000,"model":"default-model","choices":[{"index":0,"delta":{"role":"assistant","tool_calls":[{"index":0,"id":"call_1","type":"function","function":{"name":"get_weather","arguments":"{\"city\":"}}]},"finish_reason":null}]}

            data: {"id":"chatcmpl-tool","object":"chat.completion.chunk","created":1700000000,"model":"default-model","choices":[{"index":0,"delta":{"tool_calls":[{"index":0,"function":{"arguments":"\"Paris\"}"}}]},"finish_reason":"tool_calls"}]}

            data: [DONE]

            """;
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(StubHttpMessageHandler.EventStreamResponse(events)));
        var client = CreateClient(handler);

        var collected = await CollectAsync(
            client.ChatAsync(SimpleRequest(stream: true, model: null)));

        var deltas = collected.OfType<ToolCallDeltaEvent>().ToArray();
        Assert.Equal(2, deltas.Length);
        Assert.Equal(0, deltas[0].Index);
        Assert.Equal("call_1", deltas[0].Id);
        Assert.Equal("get_weather", deltas[0].Name);

        var completed = Assert.IsType<ChatCompletedEvent>(collected[^1]);
        var toolCall = Assert.Single(completed.ToolCalls);
        Assert.Equal("call_1", toolCall.Id);
        Assert.Equal("get_weather", toolCall.Name);
        Assert.Equal("""{"city":"Paris"}""", toolCall.Arguments);
        Assert.Equal("ToolCalls", completed.FinishReason);
    }

    [Fact]
    public async Task ApiFailureIsWrappedWithoutLeakingApiKey()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            var response = StubHttpMessageHandler.JsonResponse(
                """{"error":{"message":"invalid request","type":"invalid_request_error"}}""",
                HttpStatusCode.Unauthorized);
            response.Headers.Add("x-request-id", "request-123");
            return Task.FromResult(response);
        });
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<LlmClientException>(async () =>
        {
            await CollectAsync(client.ChatAsync(SimpleRequest(stream: false, model: null)));
        });

        Assert.Equal(401, exception.StatusCode);
        Assert.Equal("request-123", exception.RequestId);
        Assert.DoesNotContain("test-key", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidRequestFailsBeforeSendingHttpRequest()
    {
        var handler = new StubHttpMessageHandler(
            (_, _) => throw new InvalidOperationException("HTTP request should not be sent."));
        var client = CreateClient(handler);
        var invalidRequest = new ChatRequest { Messages = [] };

        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await CollectAsync(client.ChatAsync(invalidRequest));
        });
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void InvalidOptionsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => CreateClient(
            new StubHttpMessageHandler(
                (_, _) => throw new InvalidOperationException("HTTP request should not be sent.")),
            baseUrl: new Uri("file:///tmp/model")));
        Assert.Throws<ArgumentException>(() => CreateClient(
            new StubHttpMessageHandler(
                (_, _) => throw new InvalidOperationException("HTTP request should not be sent.")),
            apiKey: " "));
    }

    [Fact]
    public async Task CancellationIsPropagated()
    {
        var handler = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Unreachable.");
        });
        var client = CreateClient(handler);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await CollectAsync(
                client.ChatAsync(
                    SimpleRequest(stream: false, model: null),
                    cancellation.Token));
        });
    }

    private static LlmChatClient CreateClient(
        HttpMessageHandler handler,
        IReadOnlyDictionary<string, string>? headers = null,
        Uri? baseUrl = null,
        string apiKey = "test-key")
    {
        var options = new LlmClientOptions
        {
            ApiKey = apiKey,
            BaseUrl = baseUrl ?? BaseUrl,
            DefaultModel = "default-model",
            Headers = headers ?? new Dictionary<string, string>(),
        };

        return new LlmChatClient(options, handler);
    }

    private static ChatRequest SimpleRequest(bool stream, string? model)
    {
        return new ChatRequest
        {
            Messages =
            [
                new ChatMessage { Role = ChatRole.User, Content = "Hello" },
            ],
            Model = model,
            Stream = stream,
        };
    }

    private static async Task<List<ChatEvent>> CollectAsync(
        IAsyncEnumerable<ChatEvent> events,
        CancellationToken cancellationToken = default)
    {
        var result = new List<ChatEvent>();
        await foreach (var item in events.WithCancellation(cancellationToken))
        {
            result.Add(item);
        }

        return result;
    }
}
