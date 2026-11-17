# llm-client

An Aura module for calling OpenAI-compatible Chat Completions endpoints,
used only within this project. It accepts arbitrary model names, custom base
URLs, and provider-specific parameters.

## Public interfaces

Import the public types from `llm_client`:

| Interface | Usage |
| --- | --- |
| `LlmClientSettings(base_url, api_key, model=None)` | Immutable connection settings, also available as `client.settings`. |
| `LlmClient(base_url, api_key, *, model=None, timeout=60.0, max_retries=2, default_headers=None, http_client=None)` | Create a synchronous client with an explicit URL and API key. |
| `AsyncLlmClient(base_url, api_key, *, model=None, timeout=60.0, max_retries=2, default_headers=None, http_client=None)` | Create an asynchronous client with an explicit URL and API key. |
| `client.chat(messages, *, model=None, **options)` | Return a `ChatCompletion`; await the call for an asynchronous client. |
| `client.stream(messages, *, model=None, **options)` | Return `Stream[ChatCompletionChunk]` or, when awaited, `AsyncStream[ChatCompletionChunk]`. |
| `client.close()` | Close the client; await the call for an asynchronous client. |
| `client.settings` | Read the client's immutable connection settings. |
| `client.sdk` | Access other resources through the configured OpenAI SDK client. |

Both clients support context managers: use `with` for `LlmClient` and
`async with` for `AsyncLlmClient`. An injected `http_client` must be an
`httpx.Client` or `httpx.AsyncClient`, respectively.

## Configuration

Both clients require `base_url` and `api_key` arguments. The URL, key, and model
come from explicit arguments; environment variables do not supply defaults for
them. Use an absolute HTTP or HTTPS API base URL, including its prefix such as
`/v1`; do not append `/chat/completions`. For a local server without
authentication, pass a placeholder key. Supply `model` when creating the client
to set a default, or pass it to each `chat()` or `stream()` call. A request's
`model` overrides the client default.

```python
from llm_client import LlmClient

with LlmClient(
    base_url="https://api.example.com/v1",
    api_key="your-api-key",
    model="your-provider-model",
) as client:
    response = client.chat([{"role": "user", "content": "Hello"}])
    print(response.choices[0].message.content)
```

Omitting `base_url` or `api_key` raises `TypeError`. An invalid URL, blank key,
or request without a model raises `ValueError`. Use `chat()` or `stream()` to
select the response mode; passing `stream` in `options` raises `ValueError`.

## Synchronous chat

```python
from llm_client import LlmClient

with LlmClient(
    base_url="https://api.example.com/v1",
    api_key="your-api-key",
) as client:
    response = client.chat(
        [{"role": "user", "content": "Hello"}],
        model="your-provider-model",
    )
    print(response.choices[0].message.content)
```

The result is an SDK `ChatCompletion`, preserving tool calls, usage, finish
reasons, and provider-specific response fields. SDK exceptions such as
`openai.AuthenticationError`, `openai.RateLimitError`, and
`openai.APITimeoutError` propagate to the caller.

## Streaming

```python
from llm_client import LlmClient

with LlmClient(
    base_url="https://api.example.com/v1",
    api_key="your-api-key",
    model="your-provider-model",
) as client:
    with client.stream([{"role": "user", "content": "Hello"}]) as stream:
        for chunk in stream:
            for choice in chunk.choices:
                print(choice.delta.content or "", end="", flush=True)
```

Chunks are SDK `ChatCompletionChunk` objects, including tool-call deltas and
usage-only chunks with no choices. Use the stream context manager to close
connections even when stopping iteration early. If supported by the provider,
request streamed usage with `stream_options={"include_usage": True}`.

## Asynchronous chat and streaming

```python
import asyncio

from llm_client import AsyncLlmClient


async def main():
    async with AsyncLlmClient(
        base_url="https://api.example.com/v1",
        api_key="your-api-key",
        model="your-provider-model",
    ) as client:
        response = await client.chat([{"role": "user", "content": "Hello"}])
        print(response.choices[0].message.content)

        async with await client.stream(
            [{"role": "user", "content": "Tell me a story"}]
        ) as stream:
            async for chunk in stream:
                for choice in chunk.choices:
                    print(choice.delta.content or "", end="", flush=True)


asyncio.run(main())
```

## Tools, structured output, and provider extensions

Standard SDK request options pass through unchanged. For example:

```python
from llm_client import LlmClient

with LlmClient(
    base_url="https://api.example.com/v1",
    api_key="your-api-key",
    model="your-provider-model",
    timeout=120.0,
    max_retries=2,
) as client:
    response = client.chat(
        [{"role": "user", "content": "What is the weather in Shanghai?"}],
        tools=[
            {
                "type": "function",
                "function": {
                    "name": "get_weather",
                    "description": "Get the weather for a city.",
                    "parameters": {
                        "type": "object",
                        "properties": {"city": {"type": "string"}},
                        "required": ["city"],
                    },
                },
            }
        ],
        tool_choice="auto",
    )
    print(response.choices[0].message.tool_calls)
```

The host application executes tools. Add the returned assistant message
(`response.choices[0].message.model_dump(exclude_none=True)`) and a `tool`
message with its `tool_call_id` and result to the conversation for the next call.

Pass `response_format` for JSON or JSON Schema output, `extra_body` for
provider-specific JSON fields, and `extra_headers` for request headers.
Client-wide headers can be set with `default_headers`.

Compatibility requires the provider to implement the Chat Completions protocol.
Tools, images, structured output, reasoning parameters, and usage are available
only when supported by the provider and model. Supply optional parameters such
as temperature, token limits, or streamed usage only when supported by your
provider.

Use `close()` / `await close()` when not using client context managers. Closing
the wrapper also closes an injected `http_client`.
