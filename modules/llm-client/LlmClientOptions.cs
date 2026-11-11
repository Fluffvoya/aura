namespace Aura.LlmClient;

/// <summary>
/// Configuration shared by requests sent through <see cref="LlmChatClient"/>.
/// </summary>
public sealed record LlmClientOptions
{
    /// <summary>
    /// Gets the API key used to authenticate requests.
    /// </summary>
    public required string ApiKey { get; init; }

    /// <summary>
    /// Gets the absolute base URL of an OpenAI-compatible endpoint.
    /// </summary>
    public required Uri BaseUrl { get; init; }

    /// <summary>
    /// Gets the model used when a request does not override it.
    /// </summary>
    public required string DefaultModel { get; init; }

    /// <summary>
    /// Gets additional HTTP headers added to every request.
    /// </summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } =
        new Dictionary<string, string>();
}
