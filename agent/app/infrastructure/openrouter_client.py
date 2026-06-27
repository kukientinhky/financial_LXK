from __future__ import annotations

import json
from collections.abc import Mapping as MappingABC
from datetime import datetime
from typing import Any

import httpx

from app.application.dto import FinanceReasoningResult
from app.application.interfaces.llm_reasoner import FinanceReasoningError
from app.domain.finance_parser import DEFAULT_CURRENCY


OPENROUTER_CHAT_COMPLETIONS_URL = "https://openrouter.ai/api/v1/chat/completions"


class OpenRouterFinanceReasoner:
    def __init__(
        self,
        api_key: str,
        model: str,
        timeout_seconds: float = 20.0,
        transport: httpx.AsyncBaseTransport | None = None,
    ) -> None:
        if not api_key.strip():
            raise ValueError("OpenRouter API key is required.")
        if not model.strip():
            raise ValueError("OpenRouter model is required.")
        self._api_key = api_key
        self._model = model
        self._timeout_seconds = timeout_seconds
        self._transport = transport

    async def reason(self, message: str) -> FinanceReasoningResult:
        payload = {
            "model": self._model,
            "temperature": 0,
            "response_format": {"type": "json_object"},
            "messages": [
                {"role": "system", "content": _SYSTEM_PROMPT},
                {"role": "user", "content": message},
            ],
        }
        headers = {
            "Authorization": f"Bearer {self._api_key}",
            "Accept": "application/json",
            "Content-Type": "application/json",
        }

        try:
            async with httpx.AsyncClient(timeout=self._timeout_seconds, transport=self._transport) as client:
                response = await client.post(OPENROUTER_CHAT_COMPLETIONS_URL, json=payload, headers=headers)
        except httpx.RequestError as exc:
            raise FinanceReasoningError("OpenRouter request failed.") from exc

        if response.status_code >= 400:
            raise FinanceReasoningError("OpenRouter returned an error.")

        try:
            body = response.json()
        except ValueError as exc:
            raise FinanceReasoningError("OpenRouter returned invalid JSON.") from exc

        content = _extract_message_content(body)
        data = _parse_json_object(content)
        return _validated_result(message, data)


_SYSTEM_PROMPT = """
You understand Vietnamese personal finance chat messages for ExpenseCraft.
Return only one JSON object, with no Markdown, matching this shape:
{
  "intent": "income" | "expense" | "unknown",
  "amount": integer VND amount or null,
  "currency": "VND",
  "category": short Vietnamese category or null,
  "note": concise original-language note or null,
  "occurred_at": ISO-8601 datetime with timezone or null,
  "clarification": Vietnamese clarification question or null
}
Use intent "income" for money received and "expense" for money spent.
Use "unknown" when the intent, amount, or required details are missing/ambiguous.
Do not invent missing amounts. Use integer VND: 20k means 20000, 1.5 triệu means 1500000.
Only these two tool intents exist: income and expense.
""".strip()


def _extract_message_content(body: Any) -> str:
    if not isinstance(body, MappingABC):
        raise FinanceReasoningError("OpenRouter response must be an object.")
    choices = body.get("choices")
    if not isinstance(choices, list) or not choices:
        raise FinanceReasoningError("OpenRouter response has no choices.")
    first = choices[0]
    if not isinstance(first, MappingABC):
        raise FinanceReasoningError("OpenRouter choice must be an object.")
    message = first.get("message")
    if not isinstance(message, MappingABC):
        raise FinanceReasoningError("OpenRouter choice has no message.")
    content = message.get("content")
    if not isinstance(content, str) or not content.strip():
        raise FinanceReasoningError("OpenRouter message content is empty.")
    return content


def _parse_json_object(content: str) -> MappingABC[str, Any]:
    try:
        parsed = json.loads(content)
    except json.JSONDecodeError as exc:
        raise FinanceReasoningError("LLM output is not valid JSON.") from exc
    if not isinstance(parsed, MappingABC):
        raise FinanceReasoningError("LLM output must be a JSON object.")
    return parsed


def _validated_result(original_message: str, data: MappingABC[str, Any]) -> FinanceReasoningResult:
    intent = data.get("intent")
    if intent not in {"income", "expense", "unknown"}:
        raise FinanceReasoningError("LLM output has invalid intent.")

    amount = data.get("amount")
    if amount is not None:
        if isinstance(amount, bool) or not isinstance(amount, int) or amount <= 0:
            raise FinanceReasoningError("LLM output has invalid amount.")

    currency = data.get("currency") or DEFAULT_CURRENCY
    if currency != DEFAULT_CURRENCY:
        raise FinanceReasoningError("LLM output has unsupported currency.")

    category = _optional_string(data.get("category"), "category")
    note = _optional_string(data.get("note"), "note")
    clarification = _optional_string(data.get("clarification"), "clarification")
    occurred_at = _optional_datetime(data.get("occurred_at"))

    if intent in {"income", "expense"} and (amount is None or not category):
        raise FinanceReasoningError("LLM output lacks required tool fields.")

    return FinanceReasoningResult(
        original_message=original_message,
        intent=intent,
        amount=amount,
        currency=currency,
        category=category,
        note=note,
        occurred_at=occurred_at,
        clarification=clarification,
    )


def _optional_string(value: Any, field: str) -> str | None:
    if value is None:
        return None
    if not isinstance(value, str):
        raise FinanceReasoningError(f"LLM output has invalid {field}.")
    stripped = value.strip()
    return stripped or None


def _optional_datetime(value: Any) -> datetime | None:
    if value is None:
        return None
    if not isinstance(value, str) or not value.strip():
        raise FinanceReasoningError("LLM output has invalid occurred_at.")
    try:
        return datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError as exc:
        raise FinanceReasoningError("LLM output occurred_at is not ISO-8601.") from exc
