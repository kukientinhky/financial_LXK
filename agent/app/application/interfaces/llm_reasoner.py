from __future__ import annotations

from typing import Protocol

from app.application.dto import FinanceReasoningResult


class FinanceReasoningError(RuntimeError):
    pass


class FinanceReasoner(Protocol):
    async def reason(self, message: str) -> FinanceReasoningResult:
        ...
