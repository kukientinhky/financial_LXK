from __future__ import annotations

from collections.abc import Mapping as MappingABC
from typing import Any, Mapping

import httpx

from app.application.dto import ChatMessageToSave, TransactionToolRequest


class BackendClientError(RuntimeError):
    def __init__(self, detail: str) -> None:
        super().__init__(detail)
        self.detail = detail


class BackendApiError(BackendClientError):
    def __init__(self, status_code: int, detail: str) -> None:
        RuntimeError.__init__(self, f"Backend API returned {status_code}: {detail}")
        self.status_code = status_code
        self.detail = detail


class BackendTimeoutError(BackendClientError):
    pass


class BackendNetworkError(BackendClientError):
    pass


class BackendResponseError(BackendClientError):
    pass


class BackendApiClient:
    def __init__(
        self,
        base_url: str,
        timeout_seconds: float = 10.0,
        transport: httpx.AsyncBaseTransport | None = None,
    ) -> None:
        self._base_url = base_url.rstrip("/")
        self._timeout_seconds = timeout_seconds
        self._transport = transport

    async def save_message(self, authorization: str, message: ChatMessageToSave) -> Mapping[str, Any]:
        payload = {
            "role": message.role,
            "content": message.content,
            "intent": message.intent,
            "transactionId": message.transaction_id,
        }
        return await self._post("/api/chat/messages", authorization, payload)

    async def post_income_tool(self, authorization: str, transaction: TransactionToolRequest) -> Mapping[str, Any]:
        return await self._post_transaction_tool("/api/agent/tools/income", authorization, transaction)

    async def post_expense_tool(self, authorization: str, transaction: TransactionToolRequest) -> Mapping[str, Any]:
        return await self._post_transaction_tool("/api/agent/tools/expense", authorization, transaction)

    async def _post_transaction_tool(
        self,
        path: str,
        authorization: str,
        transaction: TransactionToolRequest,
    ) -> Mapping[str, Any]:
        payload = {
            "amount": transaction.amount,
            "currency": transaction.currency,
            "category": transaction.category,
            "note": transaction.note,
            "source": transaction.source,
            "occurredAt": transaction.occurred_at.isoformat(),
        }
        return await self._post(path, authorization, payload)

    async def _post(self, path: str, authorization: str, payload: Mapping[str, Any]) -> Mapping[str, Any]:
        headers = {"Authorization": authorization, "Accept": "application/json"}
        try:
            async with httpx.AsyncClient(
                base_url=self._base_url,
                timeout=self._timeout_seconds,
                follow_redirects=True,
                transport=self._transport,
            ) as client:
                response = await client.post(path, json=payload, headers=headers)
        except httpx.TimeoutException as exc:
            raise BackendTimeoutError("Backend API request timed out.") from exc
        except httpx.RequestError as exc:
            raise BackendNetworkError("Backend API is unreachable.") from exc

        if response.status_code >= 400:
            raise BackendApiError(response.status_code, _response_error_detail(response))
        if not response.content:
            return {}

        try:
            body = response.json()
        except ValueError as exc:
            raise BackendResponseError("Backend API returned invalid JSON.") from exc

        if not isinstance(body, MappingABC):
            raise BackendResponseError("Backend API returned an unexpected response body.")
        return body


def _response_error_detail(response: httpx.Response) -> str:
    if not response.content:
        return response.reason_phrase or "Backend API error."

    try:
        body = response.json()
    except ValueError:
        return _truncate_detail(response.text.strip() or response.reason_phrase or "Backend API error.")

    if isinstance(body, MappingABC):
        detail = body.get("detail") or body.get("message") or body.get("title")
        if detail is not None:
            return _truncate_detail(str(detail))

    return _truncate_detail(str(body))


def _truncate_detail(value: str, max_length: int = 500) -> str:
    return value if len(value) <= max_length else value[:max_length]
