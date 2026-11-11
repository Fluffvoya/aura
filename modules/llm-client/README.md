# llm-client

`llm-client` is a .NET 10 library for calling OpenAI-compatible chat
completion endpoints. It uses the official `OpenAI` SDK while allowing the
endpoint, API key, and model to be supplied by the host application.

## Usage

```csharp
using Aura.LlmClient;

var client = new LlmChatClient(new LlmClientOptions
{
    ApiKey = Environment.GetEnvironmentVariable("AURA_API_KEY")!,
    BaseUrl = new Uri(Environment.GetEnvironmentVariable("AURA_BASE_URL")!),
    DefaultModel = Environment.GetEnvironmentVariable("AURA_MODEL_NAME")!,
});

var request = new ChatRequest
{
    Messages =
    [
        new ChatMessage { Role = ChatRole.User, Content = "Hello" },
    ],
};

await foreach (var update in client.ChatAsync(request))
{
    switch (update)
    {
        case TextDeltaEvent text:
            Console.Write(text.Text);
            break;

        case ChatCompletedEvent completed:
            Console.WriteLine();
            Console.WriteLine($"Finish reason: {completed.FinishReason}");
            break;
    }
}
```

`BaseUrl` must be an absolute HTTP or HTTPS URL and should include any path
required by the provider, such as `/v1`. Streaming is enabled by default. Set
`Stream = false` to receive only a `ChatCompletedEvent`.

The library returns tool calls but does not execute them. Add the assistant
tool-call message and the corresponding `tool` result message to the next
`ChatRequest` after the host application executes the requested function.
