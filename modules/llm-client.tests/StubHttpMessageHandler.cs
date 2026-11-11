using System.Net;
using System.Text;

namespace Aura.LlmClient.Tests;

internal sealed record CapturedRequest(
    HttpMethod Method,
    Uri Uri,
    IReadOnlyDictionary<string, string> Headers,
    string Body);

internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<CapturedRequest, CancellationToken, Task<HttpResponseMessage>> _handler;

    public StubHttpMessageHandler(
        Func<CapturedRequest, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    public List<CapturedRequest> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        AddHeaders(headers, request.Headers);
        if (request.Content is not null)
        {
            AddHeaders(headers, request.Content.Headers);
        }
        var body = request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken);
        var capturedRequest = new CapturedRequest(
            request.Method,
            request.RequestUri!,
            headers,
            body);

        Requests.Add(capturedRequest);
        return await _handler(capturedRequest, cancellationToken);
    }

    private static void AddHeaders(
        IDictionary<string, string> destination,
        IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers)
    {
        foreach (var header in headers)
        {
            destination[header.Key] = string.Join(",", header.Value);
        }
    }

    public static HttpResponseMessage JsonResponse(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    public static HttpResponseMessage EventStreamResponse(string events)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(events, Encoding.UTF8, "text/event-stream"),
        };
    }
}
