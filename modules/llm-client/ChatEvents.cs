namespace Aura.LlmClient;

/// <summary>
/// Represents an event produced while processing a chat request.
/// </summary>
public abstract record ChatEvent;

/// <summary>
/// Contains a text fragment from a streamed response.
/// </summary>
public sealed record TextDeltaEvent(string Text) : ChatEvent;

/// <summary>
/// Contains a tool call fragment from a streamed response.
/// </summary>
public sealed record ToolCallDeltaEvent(
    int Index,
    string? Id,
    string? Name,
    string ArgumentsDelta) : ChatEvent;

/// <summary>
/// Contains the completed response.
/// </summary>
public sealed record ChatCompletedEvent(
    string? ResponseId,
    string Model,
    string Text,
    IReadOnlyList<ChatToolCall> ToolCalls,
    string? FinishReason,
    ChatUsage? Usage) : ChatEvent;
