using System.ClientModel.Primitives;

namespace Aura.LlmClient.Internal;

internal sealed class ExtraHeadersPolicy : PipelinePolicy
{
    private readonly IReadOnlyDictionary<string, string> _headers;

    public ExtraHeadersPolicy(IReadOnlyDictionary<string, string> headers)
    {
        _headers = headers;
    }

    public override void Process(
        PipelineMessage message,
        IReadOnlyList<PipelinePolicy> pipeline,
        int currentIndex)
    {
        ApplyHeaders(message);
        ProcessNext(message, pipeline, currentIndex);
    }

    public override ValueTask ProcessAsync(
        PipelineMessage message,
        IReadOnlyList<PipelinePolicy> pipeline,
        int currentIndex)
    {
        ApplyHeaders(message);
        return ProcessNextAsync(message, pipeline, currentIndex);
    }

    private void ApplyHeaders(PipelineMessage message)
    {
        foreach (var header in _headers)
        {
            message.Request.Headers.Set(header.Key, header.Value);
        }
    }
}
