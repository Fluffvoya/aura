using System.ClientModel;
using System.ClientModel.Primitives;
using System.Runtime.CompilerServices;
using Aura.LlmClient.Internal;
using OpenAI;
using OpenAI.Chat;

namespace Aura.LlmClient;

/// <summary>
/// Calls OpenAI-compatible chat completion endpoints.
/// </summary>
public sealed class LlmChatClient
{
    private readonly OpenAIClient _openAIClient;
    private readonly string _defaultModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="LlmChatClient"/> class.
    /// </summary>
    public LlmChatClient(LlmClientOptions options)
        : this(options, clientOptions: null)
    {
    }

    internal LlmChatClient(LlmClientOptions options, HttpMessageHandler? handler)
        : this(options, CreateClientOptions(options, handler))
    {
    }

    private LlmChatClient(LlmClientOptions options, OpenAIClientOptions? clientOptions)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateOptions(options);

        _defaultModel = options.DefaultModel.Trim();
        clientOptions ??= CreateClientOptions(options, handler: null);
        _openAIClient = new OpenAIClient(
            new ApiKeyCredential(options.ApiKey),
            clientOptions);
    }

    /// <summary>
    /// Sends a chat request and returns response events.
    /// </summary>
    /// <remarks>
    /// Streaming requests produce text or tool deltas followed by a completed event.
    /// Non-streaming requests produce only a completed event.
    /// </remarks>
    public IAsyncEnumerable<ChatEvent> ChatAsync(
        ChatRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ChatCoreAsync(request, cancellationToken);
    }

    private async IAsyncEnumerable<ChatEvent> ChatCoreAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var model = ChatRequestValidator.ValidateAndResolveModel(request, _defaultModel);
        var messages = MapMessages(request.Messages);
        var options = MapOptions(request);
        var chatClient = _openAIClient.GetChatClient(model);

        if (!request.Stream)
        {
            ChatCompletion completion;
            try
            {
                completion = await chatClient
                    .CompleteChatAsync(messages, options, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw WrapException(exception);
            }

            yield return MapCompletion(completion, model);
            yield break;
        }

        var toolCalls = new StreamingToolCallAccumulator();
        var text = new System.Text.StringBuilder();
        string? finishReason = null;
        ChatUsage? usage = null;
        var enumerator = chatClient
            .CompleteChatStreamingAsync(messages, options, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);

        try
        {
            while (true)
            {
                bool hasNext;
                try
                {
                    hasNext = await enumerator.MoveNextAsync().ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    throw WrapException(exception);
                }

                if (!hasNext)
                {
                    break;
                }

                var update = enumerator.Current;
                var textDelta = string.Concat(
                    update.ContentUpdate.Select(part => part.Text));
                if (textDelta.Length > 0)
                {
                    text.Append(textDelta);
                    yield return new TextDeltaEvent(textDelta);
                }

                foreach (var toolCallUpdate in update.ToolCallUpdates)
                {
                    yield return toolCalls.Apply(toolCallUpdate);
                }

                var updateFinishReason = update.FinishReason.ToString();
                if (!string.IsNullOrEmpty(updateFinishReason))
                {
                    finishReason = updateFinishReason;
                }

                if (update.Usage is not null)
                {
                    usage = MapUsage(update.Usage);
                }
            }
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }

        yield return new ChatCompletedEvent(
            ResponseId: null,
            Model: model,
            Text: text.ToString(),
            ToolCalls: toolCalls.Build(),
            FinishReason: finishReason,
            Usage: usage);
    }

    private static OpenAIClientOptions CreateClientOptions(
        LlmClientOptions options,
        HttpMessageHandler? handler)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateOptions(options);

        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = options.BaseUrl,
        };

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in options.Headers)
        {
            if (string.IsNullOrWhiteSpace(header.Key))
            {
                throw new ArgumentException(
                    "HTTP header names cannot be empty.",
                    nameof(options));
            }

            headers[header.Key] = header.Value;
        }

        if (headers.Count > 0)
        {
            clientOptions.AddPolicy(
                new ExtraHeadersPolicy(headers),
                PipelinePosition.BeforeTransport);
        }

        if (handler is not null)
        {
            clientOptions.Transport = new HttpClientPipelineTransport(
                new HttpClient(handler, disposeHandler: false));
        }

        return clientOptions;
    }

    private static void ValidateOptions(LlmClientOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ApiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.DefaultModel);

        if (options.BaseUrl is null
            || !options.BaseUrl.IsAbsoluteUri
            || (options.BaseUrl.Scheme != Uri.UriSchemeHttp
                && options.BaseUrl.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException(
                "BaseUrl must be an absolute HTTP or HTTPS URI.",
                nameof(options));
        }

        if (options.Headers is null)
        {
            throw new ArgumentException("Headers cannot be null.", nameof(options));
        }
    }

    private static IReadOnlyList<OpenAI.Chat.ChatMessage> MapMessages(
        IReadOnlyList<ChatMessage> messages)
    {
        var result = new List<OpenAI.Chat.ChatMessage>(messages.Count);
        foreach (var message in messages)
        {
            result.Add(message.Role switch
            {
                ChatRole.System => OpenAI.Chat.ChatMessage.CreateSystemMessage(message.Content!),
                ChatRole.User => OpenAI.Chat.ChatMessage.CreateUserMessage(message.Content!),
                ChatRole.Assistant when message.ToolCalls is { Count: > 0 } =>
                    OpenAI.Chat.ChatMessage.CreateAssistantMessage(
                        message.ToolCalls.Select(MapToolCall)),
                ChatRole.Assistant =>
                    OpenAI.Chat.ChatMessage.CreateAssistantMessage(message.Content!),
                ChatRole.Tool =>
                    OpenAI.Chat.ChatMessage.CreateToolMessage(message.ToolCallId!, message.Content!),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(messages),
                    message.Role,
                    "Unsupported chat role."),
            });
        }

        return result;
    }

    private static OpenAI.Chat.ChatToolCall MapToolCall(ChatToolCall toolCall)
    {
        return OpenAI.Chat.ChatToolCall.CreateFunctionToolCall(
            toolCall.Id,
            toolCall.Name,
            BinaryData.FromString(toolCall.Arguments));
    }

    private static ChatCompletionOptions MapOptions(ChatRequest request)
    {
        var options = new ChatCompletionOptions
        {
            Temperature = request.Temperature,
            TopP = request.TopP,
            MaxOutputTokenCount = request.MaxOutputTokens,
            ToolChoice = MapToolChoice(request.ToolChoice),
            AllowParallelToolCalls = request.AllowParallelToolCalls,
            ResponseFormat = MapResponseFormat(request.ResponseFormat),
        };

        if (request.StopSequences is not null)
        {
            foreach (var stopSequence in request.StopSequences)
            {
                options.StopSequences.Add(stopSequence);
            }
        }

        if (request.Tools is not null)
        {
            foreach (var tool in request.Tools)
            {
                options.Tools.Add(
                    OpenAI.Chat.ChatTool.CreateFunctionTool(
                        tool.Name,
                        tool.Description ?? string.Empty,
                        BinaryData.FromString(tool.ParametersJsonSchema),
                        tool.Strict));
            }
        }

        return options;
    }

    private static OpenAI.Chat.ChatToolChoice? MapToolChoice(ChatToolChoice? toolChoice)
    {
        return toolChoice?.Mode switch
        {
            null => null,
            ChatToolChoiceMode.Auto => OpenAI.Chat.ChatToolChoice.CreateAutoChoice(),
            ChatToolChoiceMode.None => OpenAI.Chat.ChatToolChoice.CreateNoneChoice(),
            ChatToolChoiceMode.Required => OpenAI.Chat.ChatToolChoice.CreateRequiredChoice(),
            ChatToolChoiceMode.Function =>
                OpenAI.Chat.ChatToolChoice.CreateFunctionChoice(toolChoice.FunctionName!),
            _ => throw new ArgumentOutOfRangeException(
                nameof(toolChoice),
                toolChoice.Mode,
                "Unsupported tool choice mode."),
        };
    }

    private static OpenAI.Chat.ChatResponseFormat? MapResponseFormat(
        ChatResponseFormat? responseFormat)
    {
        return responseFormat?.Kind switch
        {
            null => null,
            ChatResponseFormatKind.Text =>
                OpenAI.Chat.ChatResponseFormat.CreateTextFormat(),
            ChatResponseFormatKind.JsonObject =>
                OpenAI.Chat.ChatResponseFormat.CreateJsonObjectFormat(),
            ChatResponseFormatKind.JsonSchema =>
                OpenAI.Chat.ChatResponseFormat.CreateJsonSchemaFormat(
                    responseFormat.Name!,
                    BinaryData.FromString(responseFormat.Schema!),
                    responseFormat.Description,
                    responseFormat.Strict),
            _ => throw new ArgumentOutOfRangeException(
                nameof(responseFormat),
                responseFormat.Kind,
                "Unsupported response format."),
        };
    }

    private static ChatCompletedEvent MapCompletion(ChatCompletion completion, string model)
    {
        return new ChatCompletedEvent(
            ResponseId: completion.Id,
            Model: completion.Model ?? model,
            Text: string.Concat(completion.Content.Select(part => part.Text)),
            ToolCalls: completion.ToolCalls
                .Select(toolCall => new ChatToolCall
                {
                    Id = toolCall.Id ?? string.Empty,
                    Name = toolCall.FunctionName ?? string.Empty,
                    Arguments = toolCall.FunctionArguments?.ToString() ?? string.Empty,
                })
                .ToArray(),
            FinishReason: completion.FinishReason.ToString(),
            Usage: completion.Usage is null ? null : MapUsage(completion.Usage));
    }

    private static ChatUsage MapUsage(ChatTokenUsage usage)
    {
        return new ChatUsage
        {
            InputTokens = usage.InputTokenCount,
            OutputTokens = usage.OutputTokenCount,
            TotalTokens = usage.TotalTokenCount,
        };
    }

    private static LlmClientException WrapException(Exception exception)
    {
        if (exception is ClientResultException clientException)
        {
            var response = clientException.GetRawResponse();
            string? requestId = null;
            response?.Headers.TryGetValue("x-request-id", out requestId);

            return new LlmClientException(
                "The model request failed.",
                clientException.Status,
                requestId,
                exception);
        }

        return new LlmClientException("The model request failed.", innerException: exception);
    }
}
