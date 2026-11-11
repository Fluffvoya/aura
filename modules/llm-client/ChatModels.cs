namespace Aura.LlmClient;

/// <summary>
/// Identifies the author of a chat message.
/// </summary>
public enum ChatRole
{
    /// <summary>A system instruction.</summary>
    System,

    /// <summary>An end-user message.</summary>
    User,

    /// <summary>A prior assistant response.</summary>
    Assistant,

    /// <summary>The result of a tool call.</summary>
    Tool,
}

/// <summary>
/// Represents one message in a chat conversation.
/// </summary>
public sealed record ChatMessage
{
    /// <summary>
    /// Gets the message author.
    /// </summary>
    public required ChatRole Role { get; init; }

    /// <summary>
    /// Gets the text content of the message.
    /// </summary>
    public string? Content { get; init; }

    /// <summary>
    /// Gets the ID of the tool call that this message answers.
    /// </summary>
    public string? ToolCallId { get; init; }

    /// <summary>
    /// Gets tool calls made by an assistant message.
    /// </summary>
    public IReadOnlyList<ChatToolCall>? ToolCalls { get; init; }
}

/// <summary>
/// Describes a function tool that a model may call.
/// </summary>
public sealed record ChatToolDefinition
{
    /// <summary>
    /// Gets the function name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the function description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the JSON Schema describing the function parameters.
    /// </summary>
    public required string ParametersJsonSchema { get; init; }

    /// <summary>
    /// Gets whether strict schema adherence is requested.
    /// </summary>
    public bool? Strict { get; init; }
}

/// <summary>
/// Represents a function call made by a model.
/// </summary>
public sealed record ChatToolCall
{
    /// <summary>
    /// Gets the tool call ID.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the requested function name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the raw JSON function arguments.
    /// </summary>
    public required string Arguments { get; init; }
}

/// <summary>
/// Controls how a model may choose tools.
/// </summary>
public enum ChatToolChoiceMode
{
    /// <summary>The model chooses whether to call a tool.</summary>
    Auto,

    /// <summary>The model must not call a tool.</summary>
    None,

    /// <summary>The model must call at least one tool.</summary>
    Required,

    /// <summary>The model must call the named function.</summary>
    Function,
}

/// <summary>
/// Specifies the tool choice for a request.
/// </summary>
public sealed record ChatToolChoice
{
    /// <summary>
    /// Gets the tool choice mode.
    /// </summary>
    public required ChatToolChoiceMode Mode { get; init; }

    /// <summary>
    /// Gets the function name when <see cref="Mode"/> is <see cref="ChatToolChoiceMode.Function"/>.
    /// </summary>
    public string? FunctionName { get; init; }
}

/// <summary>
/// Identifies the requested response format.
/// </summary>
public enum ChatResponseFormatKind
{
    /// <summary>Plain text.</summary>
    Text,

    /// <summary>Any valid JSON object.</summary>
    JsonObject,

    /// <summary>JSON that follows a supplied schema.</summary>
    JsonSchema,
}

/// <summary>
/// Describes the response format requested from a model.
/// </summary>
public sealed record ChatResponseFormat
{
    private ChatResponseFormat(ChatResponseFormatKind kind)
    {
        Kind = kind;
    }

    /// <summary>
    /// Gets the response format kind.
    /// </summary>
    public ChatResponseFormatKind Kind { get; }

    /// <summary>
    /// Gets the JSON schema format name.
    /// </summary>
    public string? Name { get; private init; }

    /// <summary>
    /// Gets the JSON schema.
    /// </summary>
    public string? Schema { get; private init; }

    /// <summary>
    /// Gets the schema description.
    /// </summary>
    public string? Description { get; private init; }

    /// <summary>
    /// Gets whether strict schema adherence is requested.
    /// </summary>
    public bool? Strict { get; private init; }

    /// <summary>
    /// Creates a plain text response format.
    /// </summary>
    public static ChatResponseFormat Text() => new(ChatResponseFormatKind.Text);

    /// <summary>
    /// Creates a JSON object response format.
    /// </summary>
    public static ChatResponseFormat JsonObject() => new(ChatResponseFormatKind.JsonObject);

    /// <summary>
    /// Creates a JSON schema response format.
    /// </summary>
    public static ChatResponseFormat JsonSchema(
        string name,
        string jsonSchema,
        string? description = null,
        bool? strict = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(jsonSchema);

        return new ChatResponseFormat(ChatResponseFormatKind.JsonSchema)
        {
            Name = name,
            Schema = jsonSchema,
            Description = description,
            Strict = strict,
        };
    }
}

/// <summary>
/// Defines parameters for a single chat request.
/// </summary>
public sealed record ChatRequest
{
    /// <summary>
    /// Gets the conversation sent to the model.
    /// </summary>
    public required IReadOnlyList<ChatMessage> Messages { get; init; }

    /// <summary>
    /// Gets an optional model override.
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// Gets whether the service should stream the response. Defaults to <see langword="true"/>.
    /// </summary>
    public bool Stream { get; init; } = true;

    /// <summary>
    /// Gets the sampling temperature.
    /// </summary>
    public float? Temperature { get; init; }

    /// <summary>
    /// Gets the nucleus sampling probability.
    /// </summary>
    public float? TopP { get; init; }

    /// <summary>
    /// Gets the maximum number of output tokens.
    /// </summary>
    public int? MaxOutputTokens { get; init; }

    /// <summary>
    /// Gets up to four stop sequences.
    /// </summary>
    public IReadOnlyList<string>? StopSequences { get; init; }

    /// <summary>
    /// Gets function tools available to the model.
    /// </summary>
    public IReadOnlyList<ChatToolDefinition>? Tools { get; init; }

    /// <summary>
    /// Gets the tool choice.
    /// </summary>
    public ChatToolChoice? ToolChoice { get; init; }

    /// <summary>
    /// Gets whether the model may issue parallel tool calls.
    /// </summary>
    public bool? AllowParallelToolCalls { get; init; }

    /// <summary>
    /// Gets the requested response format.
    /// </summary>
    public ChatResponseFormat? ResponseFormat { get; init; }
}

/// <summary>
/// Represents token usage reported by the service.
/// </summary>
public sealed record ChatUsage
{
    /// <summary>
    /// Gets the number of input tokens.
    /// </summary>
    public int InputTokens { get; init; }

    /// <summary>
    /// Gets the number of output tokens.
    /// </summary>
    public int OutputTokens { get; init; }

    /// <summary>
    /// Gets the total number of tokens.
    /// </summary>
    public int TotalTokens { get; init; }
}
