from __future__ import annotations

from typing import Any, Literal

from pydantic import BaseModel, Field


class ChatRequestSchema(BaseModel):
    message: str = Field(..., min_length=1, max_length=4000)


class ChatResponseSchema(BaseModel):
    reply: str
    intent: Literal["income", "expense"] | None = None
    transaction: dict[str, Any] | None = None
    history_save_failed: bool = False
    warning: str | None = None
