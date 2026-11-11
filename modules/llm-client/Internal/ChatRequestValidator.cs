using System.Text.Json;

namespace Aura.LlmClient.Internal;

internal static class ChatRequestValidator
{
    public static string ValidateAndResolveModel(ChatRequest request, string defaultModel)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Messages is null || request.Messages.Count == 0)
        {
            throw new ArgumentException("At least one chat message is required.", nameof(request));
        }

        var model = string.IsNullOrWhiteSpace(request.Model) ? defaultModel : request.Model;
        if (string.IsNullOrWhiteSpace(model))
        {
            throw new ArgumentException("A model name is required.", nameof(request));
        }

        if (request.Temperature is < 0 or > 2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                request.Temperature,
                "Temperature must be between 0 and 2.");
        }

        if (request.TopP is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                request.TopP,
                "TopP must be between 0 and 1.");
        }

        if (request.MaxOutputTokens is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                request.MaxOutputTokens,
                "MaxOutputTokens must be greater than zero.");
        }

        if (request.StopSequences is { Count: > 4 })
        {
            throw new ArgumentException("At most four stop sequences are supported.", nameof(request));
        }

        if (request.StopSequences?.Any(string.IsNullOrEmpty) == true)
        {
            throw new ArgumentException("Stop sequences cannot be empty.", nameof(request));
        }

        ValidateMessages(request.Messages);
        ValidateTools(request);

        return model;
    }

    private static void ValidateMessages(IReadOnlyList<ChatMessage> messages)
    {
        for (var index = 0; index < messages.Count; index++)
        {
            var message = messages[index]
                ?? throw new ArgumentException($"Message {index} cannot be null.");

            switch (message.Role)
            {
                case ChatRole.System:
                case ChatRole.User:
                    if (string.IsNullOrWhiteSpace(message.Content))
                    {
                        throw new ArgumentException(
                            $"Message {index} requires non-empty content.",
                            nameof(messages));
                    }

                    break;

                case ChatRole.Assistant:
                    var hasContent = !string.IsNullOrWhiteSpace(message.Content);
                    var hasToolCalls = message.ToolCalls is { Count: > 0 };
                    if (hasContent == hasToolCalls)
                    {
                        throw new ArgumentException(
                            $"Assistant message {index} must contain either content or tool calls.",
                            nameof(messages));
                    }

                    if (message.ToolCalls is not null)
                    {
                        foreach (var toolCall in message.ToolCalls)
                        {
                            ValidateToolCall(toolCall, index);
                        }
                    }

                    break;

                case ChatRole.Tool:
                    if (string.IsNullOrWhiteSpace(message.ToolCallId))
                    {
                        throw new ArgumentException(
                            $"Tool message {index} requires a tool call ID.",
                            nameof(messages));
                    }

                    if (message.Content is null)
                    {
                        throw new ArgumentException(
                            $"Tool message {index} requires content.",
                            nameof(messages));
                    }

                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(messages),
                        message.Role,
                        "Unsupported chat role.");
            }
        }
    }

    private static void ValidateTools(ChatRequest request)
    {
        if (request.Tools is { Count: > 0 })
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var tool in request.Tools)
            {
                if (tool is null)
                {
                    throw new ArgumentException("Tool definitions cannot contain null entries.");
                }

                if (string.IsNullOrWhiteSpace(tool.Name))
                {
                    throw new ArgumentException("Tool names cannot be empty.");
                }

                if (!names.Add(tool.Name))
                {
                    throw new ArgumentException($"Duplicate tool name '{tool.Name}'.");
                }

                ValidateJson(tool.ParametersJsonSchema, "Tool parameter schemas");
            }
        }

        if (request.ToolChoice is { Mode: ChatToolChoiceMode.Function })
        {
            if (string.IsNullOrWhiteSpace(request.ToolChoice.FunctionName))
            {
                throw new ArgumentException(
                    "A function name is required for function tool choice.",
                    nameof(request));
            }

            if (request.Tools?.Any(
                    tool => string.Equals(
                        tool.Name,
                        request.ToolChoice.FunctionName,
                        StringComparison.Ordinal)) != true)
            {
                throw new ArgumentException(
                    "The selected function must be present in Tools.",
                    nameof(request));
            }
        }

        if (request.ResponseFormat?.Kind == ChatResponseFormatKind.JsonSchema)
        {
            ValidateJson(request.ResponseFormat.Schema, "Response JSON schema");
        }
    }

    private static void ValidateToolCall(ChatToolCall toolCall, int messageIndex)
    {
        if (toolCall is null)
        {
            throw new ArgumentException($"Assistant message {messageIndex} contains a null tool call.");
        }

        if (string.IsNullOrWhiteSpace(toolCall.Id)
            || string.IsNullOrWhiteSpace(toolCall.Name)
            || toolCall.Arguments is null)
        {
            throw new ArgumentException(
                $"Assistant message {messageIndex} contains an invalid tool call.");
        }
    }

    private static void ValidateJson(string? json, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException($"{fieldName} cannot be empty.");
        }

        try
        {
            using var document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            throw new ArgumentException($"{fieldName} must contain valid JSON.", exception);
        }
    }
}
