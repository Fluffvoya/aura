import asyncio
import json

import httpx
import pytest
from llm_client import AsyncLlmClient, LlmClient, LlmClientSettings
from openai import AuthenticationError, InternalServerError, RateLimitError

MESSAGES = [{"role": "user", "content": "Hello"}]
CLIENT_OPTIONS = dict(
    base_url="https://provider.example/custom/v1",
    api_key="test-key",
    model="provider/default-model",
)
TOOLS = [
    {
        "type": "function",
        "function": {
            "name": "weather",
            "parameters": {"type": "object", "properties": {}},
        },
    }
]


def completion():
    return {
        "id": "response-1",
        "object": "chat.completion",
        "created": 0,
        "model": "provider/served-model",
        "choices": [
            {
                "index": 0,
                "message": {
                    "role": "assistant",
                    "content": "Hello!",
                    "reasoning_content": "Provider-specific reasoning",
                    "tool_calls": [
                        {
                            "id": "call-1",
                            "type": "function",
                            "function": {"name": "weather", "arguments": "{}"},
                        }
                    ],
                },
                "finish_reason": "tool_calls",
            }
        ],
        "usage": {"prompt_tokens": 3, "completion_tokens": 2, "total_tokens": 5},
    }


def streaming_response():
    base = {
        "id": "response-1",
        "object": "chat.completion.chunk",
        "created": 0,
        "model": "provider/served-model",
    }
    chunks = [
        {
            **base,
            "choices": [
                {
                    "index": 0,
                    "delta": {"role": "assistant", "content": "Hello"},
                    "finish_reason": None,
                }
            ],
        },
        {
            **base,
            "choices": [
                {
                    "index": 0,
                    "delta": {
                        "tool_calls": [
                            {
                                "index": 0,
                                "id": "call-1",
                                "type": "function",
                                "function": {"name": "weather", "arguments": "{"},
                            }
                        ]
                    },
                    "finish_reason": None,
                }
            ],
        },
        {
            **base,
            "choices": [
                {
                    "index": 0,
                    "delta": {
                        "tool_calls": [{"index": 0, "function": {"arguments": "}"}}]
                    },
                    "finish_reason": "tool_calls",
                }
            ],
        },
        {
            **base,
            "choices": [],
            "usage": {"prompt_tokens": 3, "completion_tokens": 2, "total_tokens": 5},
        },
    ]
    content = "".join(f"data: {json.dumps(chunk)}\n\n" for chunk in chunks)
    return httpx.Response(
        200,
        headers={"content-type": "text/event-stream"},
        content=content + "data: [DONE]\n\n",
    )


def assert_chunks(chunks):
    assert chunks[0].choices[0].delta.content == "Hello"
    assert chunks[1].choices[0].delta.tool_calls[0].function.name == "weather"
    assert chunks[1].choices[0].delta.tool_calls[0].function.arguments == "{"
    assert chunks[2].choices[0].delta.tool_calls[0].function.arguments == "}"
    assert chunks[2].choices[0].finish_reason == "tool_calls"
    assert chunks[3].choices == []
    assert chunks[3].usage.total_tokens == 5


def test_settings_normalization_and_secret_redaction():
    settings = LlmClientSettings(
        base_url=" https://provider.example/v1 ",
        api_key=" secret-key ",
        model=" provider-model ",
    )
    assert settings.base_url == "https://provider.example/v1"
    assert settings.api_key == "secret-key"
    assert settings.model == "provider-model"
    assert "secret-key" not in repr(settings)


@pytest.fixture
def conflicting_environment(monkeypatch):
    for prefix in ("AURA", "OPENAI"):
        monkeypatch.setenv(f"{prefix}_BASE_URL", "https://environment.example/v1")
        monkeypatch.setenv(f"{prefix}_API_KEY", "environment-key")
    monkeypatch.setenv("AURA_MODEL_NAME", "environment-model")
    monkeypatch.setenv("OPENAI_MODEL", "environment-model")


@pytest.mark.parametrize("client_type", [LlmClient, AsyncLlmClient])
@pytest.mark.parametrize(
    "options",
    [{}, {"base_url": "https://example.com/v1"}, {"api_key": "key"}],
)
def test_connection_arguments_required(client_type, options, conflicting_environment):
    with pytest.raises(TypeError, match="required positional argument"):
        client_type(**options)


@pytest.mark.parametrize("asynchronous", [False, True])
def test_explicit_configuration_ignores_environment(
    asynchronous, conflicting_environment
):
    def handle(request):
        assert request.url == "https://provider.example/custom/v1/chat/completions"
        assert request.headers["authorization"] == "Bearer test-key"
        assert json.loads(request.content)["model"] == CLIENT_OPTIONS["model"]
        return httpx.Response(200, json=completion())

    transport = httpx.MockTransport(handle)
    if asynchronous:

        async def run():
            async with AsyncLlmClient(
                **CLIENT_OPTIONS, http_client=httpx.AsyncClient(transport=transport)
            ) as client:
                await client.chat(MESSAGES)

        asyncio.run(run())
    else:
        with LlmClient(
            **CLIENT_OPTIONS, http_client=httpx.Client(transport=transport)
        ) as client:
            client.chat(MESSAGES)


@pytest.mark.parametrize("client_type", [LlmClientSettings, LlmClient, AsyncLlmClient])
@pytest.mark.parametrize(
    "url", ["", "ftp://example.com", "example.com/v1", "https:///v1", "https://["]
)
def test_invalid_url(client_type, url):
    with pytest.raises(ValueError, match="base_url"):
        client_type(base_url=url, api_key="key")


@pytest.mark.parametrize("client_type", [LlmClientSettings, LlmClient, AsyncLlmClient])
def test_empty_key(client_type):
    with pytest.raises(ValueError, match="api_key"):
        client_type(base_url="https://example.com", api_key=" ")


@pytest.mark.parametrize("model", ["vendor/model-name", "local-model:latest"])
def test_chat_passes_endpoint_model_options_and_response_fields(model):
    requests = []

    def handle(request):
        requests.append(request)
        return httpx.Response(200, json=completion())

    http = httpx.Client(transport=httpx.MockTransport(handle))
    with LlmClient(
        **CLIENT_OPTIONS, http_client=http, default_headers={"x-provider": "custom"}
    ) as client:
        response = client.chat(
            MESSAGES,
            model=model,
            tools=TOOLS,
            tool_choice="auto",
            response_format={"type": "json_object"},
            extra_body={"enable_thinking": False},
            extra_headers={"x-request-option": "enabled"},
        )
    request = requests[0]
    assert request.url == "https://provider.example/custom/v1/chat/completions"
    assert request.headers["authorization"] == "Bearer test-key"
    assert request.headers["x-provider"] == "custom"
    assert request.headers["x-request-option"] == "enabled"
    assert json.loads(request.content) == {
        "messages": MESSAGES,
        "model": model,
        "stream": False,
        "tools": TOOLS,
        "tool_choice": "auto",
        "response_format": {"type": "json_object"},
        "enable_thinking": False,
    }
    assert response.choices[0].message.content == "Hello!"
    assert response.choices[0].message.tool_calls[0].function.name == "weather"
    assert (
        response.choices[0].message.reasoning_content == "Provider-specific reasoning"
    )
    assert response.model == "provider/served-model"
    assert response.usage.total_tokens == 5
    assert http.is_closed


def test_default_model_and_no_optional_parameters():
    def handle(request):
        assert json.loads(request.content) == {
            "messages": MESSAGES,
            "model": CLIENT_OPTIONS["model"],
            "stream": False,
        }
        return httpx.Response(200, json=completion())

    with LlmClient(
        **CLIENT_OPTIONS,
        http_client=httpx.Client(transport=httpx.MockTransport(handle)),
    ) as c:
        c.chat(MESSAGES)


@pytest.mark.parametrize("asynchronous", [False, True])
def test_model_required_before_network_request(asynchronous, conflicting_environment):
    def handle(request):
        pytest.fail("A request without a model must fail before network access")

    transport = httpx.MockTransport(handle)
    if asynchronous:

        async def run():
            async with AsyncLlmClient(
                CLIENT_OPTIONS["base_url"],
                CLIENT_OPTIONS["api_key"],
                http_client=httpx.AsyncClient(transport=transport),
            ) as client:
                with pytest.raises(ValueError, match="model"):
                    await client.chat(MESSAGES)
                with pytest.raises(ValueError, match="model"):
                    await client.stream(MESSAGES, model=" ")

        asyncio.run(run())
    else:
        with LlmClient(
            CLIENT_OPTIONS["base_url"],
            CLIENT_OPTIONS["api_key"],
            http_client=httpx.Client(transport=transport),
        ) as client:
            with pytest.raises(ValueError, match="model"):
                client.chat(MESSAGES)
            with pytest.raises(ValueError, match="model"):
                client.stream(MESSAGES, model=" ")


def test_stream_preserves_tool_fragments_and_usage():
    def handle(request):
        assert json.loads(request.content)["stream"] is True
        return streaming_response()

    http = httpx.Client(transport=httpx.MockTransport(handle))
    with LlmClient(**CLIENT_OPTIONS, http_client=http) as client:
        with client.stream(MESSAGES) as stream:
            assert_chunks(list(stream))
        assert stream.response.is_closed
    assert http.is_closed


def test_early_stream_exit_closes_connection():
    class TrackedStream(httpx.SyncByteStream):
        closed = False

        def __iter__(self):
            yield streaming_response().content

        def close(self):
            self.closed = True

    body = TrackedStream()
    transport = httpx.MockTransport(
        lambda request: httpx.Response(
            200, headers={"content-type": "text/event-stream"}, stream=body
        )
    )
    with LlmClient(
        **CLIENT_OPTIONS, http_client=httpx.Client(transport=transport)
    ) as client:
        with client.stream(MESSAGES) as stream:
            assert next(iter(stream)).choices[0].delta.content == "Hello"
        assert body.closed


@pytest.mark.parametrize(
    ("status", "error"),
    [(401, AuthenticationError), (429, RateLimitError), (500, InternalServerError)],
)
def test_sdk_errors_preserve_status_and_request_id(status, error):
    transport = httpx.MockTransport(
        lambda request: httpx.Response(
            status,
            headers={"x-request-id": "request-1"},
            json={"error": {"message": "Provider error", "type": "provider_error"}},
        )
    )
    with LlmClient(
        **CLIENT_OPTIONS, max_retries=0, http_client=httpx.Client(transport=transport)
    ) as client:
        with pytest.raises(error) as caught:
            client.chat(MESSAGES)
    assert caught.value.status_code == status
    assert caught.value.request_id == "request-1"


@pytest.mark.parametrize("method", ["chat", "stream"])
def test_conflicting_stream_option_fails_clearly(method):
    with LlmClient(**CLIENT_OPTIONS) as client:
        with pytest.raises(ValueError, match="stream"):
            getattr(client, method)(MESSAGES, stream=True)


def test_async_chat_and_stream():
    requests = []

    def handle(request):
        requests.append(request)
        if json.loads(request.content)["stream"]:
            return streaming_response()
        return httpx.Response(200, json=completion())

    async def run():
        http = httpx.AsyncClient(transport=httpx.MockTransport(handle))
        async with AsyncLlmClient(**CLIENT_OPTIONS, http_client=http) as client:
            response = await client.chat(
                MESSAGES, model="async/custom-model", extra_body={"custom_option": True}
            )
            assert response.choices[0].message.content == "Hello!"
            assert response.choices[0].message.tool_calls[0].id == "call-1"
            async with await client.stream(MESSAGES) as stream:
                assert_chunks([chunk async for chunk in stream])
            assert stream.response.is_closed
        assert http.is_closed

    asyncio.run(run())
    assert requests[0].url == "https://provider.example/custom/v1/chat/completions"
    assert requests[0].headers["authorization"] == "Bearer test-key"
    assert json.loads(requests[0].content)["model"] == "async/custom-model"
    assert json.loads(requests[0].content)["custom_option"] is True
    assert json.loads(requests[1].content)["model"] == CLIENT_OPTIONS["model"]


def test_async_error_and_early_stream_exit():
    class TrackedStream(httpx.AsyncByteStream):
        closed = False

        async def __aiter__(self):
            yield streaming_response().content

        async def aclose(self):
            self.closed = True

    body = TrackedStream()

    def handle(request):
        if json.loads(request.content)["stream"]:
            return httpx.Response(
                200, headers={"content-type": "text/event-stream"}, stream=body
            )
        return httpx.Response(401, json={"error": {"message": "Invalid key"}})

    async def run():
        http = httpx.AsyncClient(transport=httpx.MockTransport(handle))
        async with AsyncLlmClient(
            **CLIENT_OPTIONS, http_client=http, max_retries=0
        ) as client:
            with pytest.raises(AuthenticationError):
                await client.chat(MESSAGES)
            async with await client.stream(MESSAGES) as stream:
                async for chunk in stream:
                    assert chunk.choices[0].delta.content == "Hello"
                    break
            assert body.closed
        assert http.is_closed

    asyncio.run(run())
