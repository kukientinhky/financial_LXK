from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime
from enum import Enum


class FinanceIntent(str, Enum):
    INCOME = "income"
    EXPENSE = "expense"


@dataclass(frozen=True)
class ParseIssue:
    code: str
    message: str


@dataclass(frozen=True)
class ParsedFinanceMessage:
    original_message: str
    intent: FinanceIntent | None
    amount: int | None
    currency: str
    category: str | None
    note: str
    occurred_at: datetime
    issue: ParseIssue | None = None

    @property
    def can_create_transaction(self) -> bool:
        return self.issue is None and self.intent is not None and self.amount is not None and self.category is not None
