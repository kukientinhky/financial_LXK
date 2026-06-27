from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime
from typing import Any, Mapping


@dataclass(frozen=True)
class ChatMessageToSave:
    role: str
    content: str
    intent: str | None = None
    transaction_id: str | None = None


@dataclass(frozen=True)
class TransactionToolRequest:
    amount: int
    currency: str
    category: str
    note: str | None
    source: str
    occurred_at: datetime


@dataclass(frozen=True)
class FinanceReasoningResult:
    original_message: str
    intent: str
    amount: int | None
    currency: str
    category: str | None
    note: str | None
    occurred_at: datetime | None
    clarification: str | None = None


@dataclass(frozen=True)
class ChatResult:
    reply: str
    intent: str | None
    transaction: Mapping[str, Any] | None
    history_save_failed: bool = False
    warning: str | None = None
