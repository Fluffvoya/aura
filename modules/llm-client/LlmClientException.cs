namespace Aura.LlmClient;

/// <summary>
/// Represents a failed model request.
/// </summary>
public sealed class LlmClientException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LlmClientException"/> class.
    /// </summary>
    public LlmClientException(
        string message,
        int? statusCode = null,
        string? requestId = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        RequestId = requestId;
    }

    /// <summary>
    /// Gets the HTTP status code, if one was received.
    /// </summary>
    public int? StatusCode { get; }

    /// <summary>
    /// Gets the service request ID, if one was returned.
    /// </summary>
    public string? RequestId { get; }
}
