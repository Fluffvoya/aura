"""Synchronous and asynchronous clients for OpenAI-compatible chat APIs."""

from __future__ import annotations

from collections.abc import Mapping, Sequence
from dataclasses import dataclass, field
from typing import Any
from urllib.parse import urlsplit

import httpx
from openai import AsyncOpenAI, AsyncStream, OpenAI, Stream
from openai.types.chat import (
    ChatCompletion,
    ChatCompletionChunk,
    ChatCompletionMessageParam,
)

__all__ = ["AsyncLlmClient", "LlmClient", "LlmClientSettings"]


@dataclass(frozen=True)
class LlmClientSettings:
    """Connection settings; API keys are excluded from the representation."""

    base_url: str
    api_key: str = field(repr=False)
    model: str | None = None

    def __post_init__(self) -> None:
        for name in ("base_url", "api_key"):
            value = getattr(self, name).strip()
            if not value:
                raise ValueError(f"{name} must not be empty")
            object.__setattr__(self, name, value)
        try:
            url = urlsplit(self.base_url)
            valid_url = url.scheme in {"http", "https"} and bool(url.hostname)
        except ValueError:
            valid_url = False
        if not valid_url:
            raise ValueError("base_url must be an absolute HTTP or HTTPS URL")
        if self.model is not None:
            object.__setattr__(self, "model", self.model.strip() or None)


def _request_params(
    settings: LlmClientSettings,
    messages: Sequence[ChatCompletionMessageParam],
    model: str | None,
    options: dict[str, Any],
) -> dict[str, Any]:
    resolved_model = settings.model if model is None else model.strip()
    if not resolved_model:
        raise ValueError("Pass model to the client constructor or this request")
    if "stream" in options:
        raise ValueError("Use chat() for full responses or stream() for streaming")
    return {"messages": messages, "model": resolved_model, **options}


class LlmClient:
    """Call any provider implementing OpenAI's Chat Completions protocol.

    Request options are forwarded to the SDK without adding model-specific
    defaults. Use extra_body for provider-specific request fields. SDK errors
    and response types are preserved, including tool calls and usage details.
    """

    def __init__(
        self,
        base_url: str,
        api_key: str,
        *,
        model: str | None = None,
        timeout: float = 60.0,
        max_retries: int = 2,
        default_headers: Mapping[str, str] | None = None,
        http_client: httpx.Client | None = None,
    ) -> None:
        self.settings = LlmClientSettings(
            base_url=base_url, api_key=api_key, model=model
        )
        self.sdk = OpenAI(
            base_url=self.settings.base_url,
            api_key=self.settings.api_key,
            timeout=timeout,
            max_retries=max_retries,
            default_headers=default_headers,
            http_client=http_client,
        )

    def chat(
        self,
        messages: Sequence[ChatCompletionMessageParam],
        *,
        model: str | None = None,
        **options: Any,
    ) -> ChatCompletion:
        """Return a complete response, including tool calls and provider fields."""
        return self.sdk.chat.completions.create(
            **_request_params(self.settings, messages, model, options), stream=False
        )

    def stream(
        self,
        messages: Sequence[ChatCompletionMessageParam],
        *,
        model: str | None = None,
        **options: Any,
    ) -> Stream[ChatCompletionChunk]:
        """Return an SDK stream; use it as a context manager for early exits."""
        return self.sdk.chat.completions.create(
            **_request_params(self.settings, messages, model, options), stream=True
        )

    def close(self) -> None:
        """Close the underlying HTTP client, including an injected client."""
        self.sdk.close()

    def __enter__(self) -> LlmClient:
        return self

    def __exit__(self, *exc: Any) -> None:
        self.close()


class AsyncLlmClient:
    """Asynchronous counterpart of LlmClient, with the same request options."""

    def __init__(
        self,
        base_url: str,
        api_key: str,
        *,
        model: str | None = None,
        timeout: float = 60.0,
        max_retries: int = 2,
        default_headers: Mapping[str, str] | None = None,
        http_client: httpx.AsyncClient | None = None,
    ) -> None:
        self.settings = LlmClientSettings(
            base_url=base_url, api_key=api_key, model=model
        )
        self.sdk = AsyncOpenAI(
            base_url=self.settings.base_url,
            api_key=self.settings.api_key,
            timeout=timeout,
            max_retries=max_retries,
            default_headers=default_headers,
            http_client=http_client,
        )

    async def chat(
        self,
        messages: Sequence[ChatCompletionMessageParam],
        *,
        model: str | None = None,
        **options: Any,
    ) -> ChatCompletion:
        """Await a complete response."""
        return await self.sdk.chat.completions.create(
            **_request_params(self.settings, messages, model, options), stream=False
        )

    async def stream(
        self,
        messages: Sequence[ChatCompletionMessageParam],
        *,
        model: str | None = None,
        **options: Any,
    ) -> AsyncStream[ChatCompletionChunk]:
        """Await an SDK stream; use async with to close it on early exits."""
        return await self.sdk.chat.completions.create(
            **_request_params(self.settings, messages, model, options), stream=True
        )

    async def close(self) -> None:
        """Close the underlying HTTP client, including an injected client."""
        await self.sdk.close()

    async def __aenter__(self) -> AsyncLlmClient:
        return self

    async def __aexit__(self, *exc: Any) -> None:
        await self.close()
