from __future__ import annotations

from typing import Any, Mapping, Protocol

from app.application.dto import TransactionToolRequest


class McpTransactionTools(Protocol):
    async def income(self, authorization: str, transaction: TransactionToolRequest) -> Mapping[str, Any]:
        ...

    async def expense(self, authorization: str, transaction: TransactionToolRequest) -> Mapping[str, Any]:
        ...
