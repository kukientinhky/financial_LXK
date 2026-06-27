from __future__ import annotations

from typing import Any, Mapping, Protocol

from app.application.dto import ChatMessageToSave


class ChatHistoryClient(Protocol):
    async def save_message(self, authorization: str, message: ChatMessageToSave) -> Mapping[str, Any]:
        ...
