from __future__ import annotations

from typing import Any, Mapping

from app.application.dto import TransactionToolRequest
from app.infrastructure.backend_api_client import BackendApiClient


class InternalMcpToolServer:
    """Internal MCP-like tool server exposing exactly two write tools: income and expense."""

    write_tool_names = ("income", "expense")

    def __init__(self, backend_api: BackendApiClient) -> None:
        self._backend_api = backend_api

    async def income(self, authorization: str, transaction: TransactionToolRequest) -> Mapping[str, Any]:
        return await self._backend_api.post_income_tool(authorization, transaction)

    async def expense(self, authorization: str, transaction: TransactionToolRequest) -> Mapping[str, Any]:
        return await self._backend_api.post_expense_tool(authorization, transaction)


class InternalMcpToolClient:
    """Small client facade for the internal MCP-like tool server."""

    def __init__(self, server: InternalMcpToolServer) -> None:
        self._server = server

    async def income(self, authorization: str, transaction: TransactionToolRequest) -> Mapping[str, Any]:
        return await self._server.income(authorization, transaction)

    async def expense(self, authorization: str, transaction: TransactionToolRequest) -> Mapping[str, Any]:
        return await self._server.expense(authorization, transaction)
