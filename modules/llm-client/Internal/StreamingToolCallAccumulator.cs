using OpenAI.Chat;

namespace Aura.LlmClient.Internal;

internal sealed class StreamingToolCallAccumulator
{
    private readonly Dictionary<int, ToolCallState> _toolCalls = new();

    public ToolCallDeltaEvent Apply(StreamingChatToolCallUpdate update)
    {
        if (!_toolCalls.TryGetValue(update.Index, out var state))
        {
            state = new ToolCallState();
            _toolCalls.Add(update.Index, state);
        }

        if (!string.IsNullOrEmpty(update.ToolCallId))
        {
            state.Id = update.ToolCallId;
        }

        if (!string.IsNullOrEmpty(update.FunctionName))
        {
            state.Name = update.FunctionName;
        }

        var argumentsUpdate = update.FunctionArgumentsUpdate?.ToString() ?? string.Empty;
        state.Arguments.Append(argumentsUpdate);

        return new ToolCallDeltaEvent(
            update.Index,
            update.ToolCallId,
            update.FunctionName,
            argumentsUpdate);
    }

    public IReadOnlyList<ChatToolCall> Build()
    {
        return _toolCalls
            .OrderBy(pair => pair.Key)
            .Select(pair => new ChatToolCall
            {
                Id = pair.Value.Id ?? string.Empty,
                Name = pair.Value.Name ?? string.Empty,
                Arguments = pair.Value.Arguments.ToString(),
            })
            .ToArray();
    }

    private sealed class ToolCallState
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public System.Text.StringBuilder Arguments { get; } = new();
    }
}
