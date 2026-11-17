# Aura

A uv-managed Python workspace. Requires Python 3.11 or later; the repository
pins Python 3.14 for local development.

## Modules

All modules are used only within Aura and live as peers directly under
`modules/`.

- [llm-client](modules/llm-client/README.md): synchronous and asynchronous clients
  for OpenAI-compatible Chat Completions APIs using the official OpenAI SDK.

## Usage

Pass the provider's API base URL and API key explicitly when creating a client.
Set a default model on the client or supply one for each request.

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

See the module documentation for examples of chat, streaming, async calls,
tools, and provider-specific options.

## Verification

```bash
uv run --locked pytest
uv run --locked ruff check .
uv run --locked ruff format --check .
uv build --package llm-client
```

Tests mock HTTP responses and require no model-service network access or real
credentials.
