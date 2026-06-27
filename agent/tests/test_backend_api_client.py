from __future__ import annotations

import httpx
import pytest

from datetime import datetime
from zoneinfo import ZoneInfo

from app.application.dto import ChatMessageToSave, TransactionToolRequest
from app.infrastructure.backend_api_client import (
    BackendApiClient,
    BackendApiError,
    BackendNetworkError,
    BackendResponseError,
    BackendTimeoutError,
)


@pytest.mark.asyncio
async def test_backend_http_error_is_wrapped() -> None:
    transport = httpx.MockTransport(lambda request: httpx.Response(500, json={"detail": "boom"}))
    client = BackendApiClient("http://backend.test", transport=transport)

    with pytest.raises(BackendApiError) as exc_info:
        await client.save_message("Bearer token", ChatMessageToSave(role="user", content="hello"))

    assert exc_info.value.status_code == 500
    assert exc_info.value.detail == "boom"


@pytest.mark.asyncio
async def test_backend_network_error_is_wrapped() -> None:
    def raise_connect_error(request: httpx.Request) -> httpx.Response:
        raise httpx.ConnectError("connection failed", request=request)

    client = BackendApiClient("http://backend.test", transport=httpx.MockTransport(raise_connect_error))

    with pytest.raises(BackendNetworkError):
        await client.save_message("Bearer token", ChatMessageToSave(role="user", content="hello"))


@pytest.mark.asyncio
async def test_backend_timeout_is_wrapped() -> None:
    def raise_timeout(request: httpx.Request) -> httpx.Response:
        raise httpx.ReadTimeout("timed out", request=request)

    client = BackendApiClient("http://backend.test", transport=httpx.MockTransport(raise_timeout))

    with pytest.raises(BackendTimeoutError):
        await client.save_message("Bearer token", ChatMessageToSave(role="user", content="hello"))


@pytest.mark.asyncio
async def test_backend_invalid_json_is_wrapped() -> None:
    transport = httpx.MockTransport(lambda request: httpx.Response(200, text="not-json"))
    client = BackendApiClient("http://backend.test", transport=transport)

    with pytest.raises(BackendResponseError):
        await client.save_message("Bearer token", ChatMessageToSave(role="user", content="hello"))


@pytest.mark.asyncio
async def test_backend_uses_english_transaction_tool_paths() -> None:
    paths: list[str] = []

    def record_path(request: httpx.Request) -> httpx.Response:
        paths.append(request.url.path)
        return httpx.Response(200, json={"id": "transaction-1"})

    client = BackendApiClient("http://backend.test", transport=httpx.MockTransport(record_path))
    transaction = TransactionToolRequest(
        amount=20_000,
        currency="VND",
        category="Ăn uống",
        note="ăn sáng",
        source="agent",
        occurred_at=datetime(2026, 6, 27, 9, 0, tzinfo=ZoneInfo("Asia/Ho_Chi_Minh")),
    )

    await client.post_income_tool("Bearer token", transaction)
    await client.post_expense_tool("Bearer token", transaction)

    assert paths == ["/api/agent/tools/income", "/api/agent/tools/expense"]
