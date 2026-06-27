from __future__ import annotations

from dataclasses import replace
from typing import Any, Mapping

from datetime import datetime

from app.application.dto import ChatMessageToSave, ChatResult, FinanceReasoningResult, TransactionToolRequest
from app.application.interfaces.chat_history import ChatHistoryClient
from app.application.interfaces.llm_reasoner import FinanceReasoner, FinanceReasoningError
from app.application.interfaces.mcp_tools import McpTransactionTools
from app.domain.finance_parser import DEFAULT_CURRENCY, VIETNAM_TZ, VietnameseFinanceParser
from app.domain.models import FinanceIntent, ParsedFinanceMessage, ParseIssue


class ChatService:
    def __init__(
        self,
        parser: VietnameseFinanceParser,
        chat_history: ChatHistoryClient,
        tools: McpTransactionTools,
        reasoner: FinanceReasoner | None = None,
    ) -> None:
        self._parser = parser
        self._chat_history = chat_history
        self._tools = tools
        self._reasoner = reasoner

    async def handle_message(self, message: str, authorization: str) -> ChatResult:
        parsed = await self._understand_message(message)
        history_intent = _intent_for_history(parsed)

        await self._chat_history.save_message(
            authorization,
            ChatMessageToSave(
                role="user",
                content=parsed.original_message,
                intent=history_intent,
            ),
        )

        if not parsed.can_create_transaction:
            reply = _clarification_reply(parsed)
            await self._chat_history.save_message(
                authorization,
                ChatMessageToSave(role="assistant", content=reply, intent=history_intent),
            )
            return ChatResult(reply=reply, intent=_intent_for_response(parsed), transaction=None)

        field_issue = _transaction_field_issue(parsed.category, parsed.note)
        if field_issue is not None:
            parsed = replace(parsed, issue=field_issue)
            reply = _clarification_reply(parsed)
            await self._chat_history.save_message(
                authorization,
                ChatMessageToSave(role="assistant", content=reply, intent=history_intent),
            )
            return ChatResult(reply=reply, intent=_intent_for_response(parsed), transaction=None)

        tool_request = TransactionToolRequest(
            amount=parsed.amount or 0,
            currency=parsed.currency,
            category=parsed.category or "Khác",
            note=parsed.note,
            source="agent",
            occurred_at=parsed.occurred_at,
        )

        if parsed.intent == FinanceIntent.INCOME:
            transaction = await self._tools.income(authorization, tool_request)
        else:
            transaction = await self._tools.expense(authorization, tool_request)

        reply = _success_reply(parsed)
        history_save_failed = False
        warning = None
        try:
            await self._chat_history.save_message(
                authorization,
                ChatMessageToSave(
                    role="assistant",
                    content=reply,
                    intent=history_intent,
                    transaction_id=_transaction_id(transaction),
                ),
            )
        except Exception:
            history_save_failed = True
            warning = "Giao dịch đã được ghi nhưng không lưu được phản hồi vào lịch sử."

        return ChatResult(
            reply=reply,
            intent=_intent_for_response(parsed),
            transaction=transaction,
            history_save_failed=history_save_failed,
            warning=warning,
        )

    async def _understand_message(self, message: str) -> ParsedFinanceMessage:
        if self._reasoner is None:
            return self._parser.parse(message)

        try:
            decision = await self._reasoner.reason(message)
        except FinanceReasoningError:
            return _invalid_reasoning_result(message)

        safety_issue = _deterministic_safety_issue(self._parser, message)
        parsed = _parsed_from_reasoning(decision, message)
        if safety_issue is not None:
            return replace(parsed, intent=None, issue=safety_issue)
        return parsed


def _intent_for_history(parsed: ParsedFinanceMessage) -> str:
    return parsed.intent.value if parsed.intent else "unknown"


def _intent_for_response(parsed: ParsedFinanceMessage) -> str | None:
    return parsed.intent.value if parsed.intent else None


def _clarification_reply(parsed: ParsedFinanceMessage) -> str:
    code = parsed.issue.code if parsed.issue else "unknown"

    if (
        code in {"unknown", "invalid_llm_output", "invalid_llm_intent", "missing_category", "invalid_transaction_field"}
        and parsed.issue
        and parsed.issue.message
    ):
        return parsed.issue.message

    if code in {"missing_intent_and_amount", "ambiguous_intent_and_missing_amount"}:
        return "Bạn muốn ghi khoản này là thu hay chi và số tiền là bao nhiêu?"
    if code == "missing_amount":
        return "Bạn cho mình biết số tiền cần ghi nhận là bao nhiêu nhé."
    if code == "invalid_explicit_date":
        return (
            "Ngày giao dịch không hợp lệ. "
            "Bạn cho mình biết lại ngày theo định dạng dd/mm hoặc dd/mm/yyyy nhé."
        )
    if code in {"ambiguous_intent", "low_confidence_intent"}:
        return "Bạn muốn ghi khoản này là thu hay chi?"
    if code == "low_confidence_intent_and_missing_amount":
        return "Bạn muốn ghi khoản này là thu hay chi và số tiền là bao nhiêu?"
    if code == "missing_intent":
        return "Bạn muốn ghi khoản này là thu hay chi?"
    return "Mình chưa hiểu rõ khoản này. Bạn nói rõ loại giao dịch và số tiền giúp mình nhé."


def _success_reply(parsed: ParsedFinanceMessage) -> str:
    intent_text = "thu" if parsed.intent == FinanceIntent.INCOME else "chi"
    category = parsed.category or "Khác"
    return f"Đã ghi khoản {intent_text} {_format_vnd(parsed.amount or 0)} cho {category}."


def _format_vnd(amount: int) -> str:
    return f"{amount:,}".replace(",", ".") + "₫"


def _transaction_id(transaction: Mapping[str, Any]) -> str | None:
    value = transaction.get("id") or transaction.get("Id")
    return str(value) if value else None


def _parsed_from_reasoning(decision: FinanceReasoningResult, original_message: str) -> ParsedFinanceMessage:
    intent = _coerce_intent(decision.intent)
    occurred_at = _coerce_occurred_at(decision.occurred_at)
    issue = _issue_from_decision(decision, intent)
    note = (decision.note or original_message).strip()
    return ParsedFinanceMessage(
        original_message=original_message.strip(),
        intent=intent,
        amount=decision.amount,
        currency=decision.currency or DEFAULT_CURRENCY,
        category=decision.category.strip() if decision.category else None,
        note=note,
        occurred_at=occurred_at,
        issue=issue,
    )


def _coerce_intent(value: str) -> FinanceIntent | None:
    if value == FinanceIntent.INCOME.value:
        return FinanceIntent.INCOME
    if value == FinanceIntent.EXPENSE.value:
        return FinanceIntent.EXPENSE
    return None


def _coerce_occurred_at(value: datetime | None) -> datetime:
    if value is None:
        return datetime.now(VIETNAM_TZ)
    if value.tzinfo is None:
        return value.replace(tzinfo=VIETNAM_TZ)
    return value.astimezone(VIETNAM_TZ)


def _issue_from_decision(decision: FinanceReasoningResult, intent: FinanceIntent | None) -> ParseIssue | None:
    if decision.intent == "unknown":
        return ParseIssue("unknown", decision.clarification or "Mình chưa hiểu rõ khoản này. Bạn nói rõ loại giao dịch và số tiền giúp mình nhé.")
    if intent is None:
        return ParseIssue("invalid_llm_intent", "Mình chưa hiểu rõ khoản này. Bạn nói rõ loại giao dịch và số tiền giúp mình nhé.")
    if decision.amount is None or decision.amount <= 0:
        return ParseIssue("missing_amount", decision.clarification or "Bạn cho mình biết số tiền cần ghi nhận là bao nhiêu nhé.")
    if not decision.category or not decision.category.strip():
        return ParseIssue("missing_category", decision.clarification or "Bạn cho mình biết khoản này thuộc nhóm nào nhé.")
    field_issue = _transaction_field_issue(decision.category.strip(), decision.note.strip() if decision.note else None)
    if field_issue is not None:
        return field_issue
    return None


def _deterministic_safety_issue(parser: VietnameseFinanceParser, message: str) -> ParseIssue | None:
    deterministic = parser.parse(message)
    if deterministic.issue and deterministic.issue.code in {"ambiguous_intent", "ambiguous_intent_and_missing_amount"}:
        return ParseIssue("ambiguous_intent", "Bạn muốn ghi khoản này là thu hay chi?")
    return None


def _transaction_field_issue(category: str | None, note: str | None) -> ParseIssue | None:
    if category is not None and len(category) > 100:
        return ParseIssue(
            "invalid_transaction_field",
            "Mình chưa hiểu rõ nhóm giao dịch. Bạn nói lại nhóm ngắn gọn hơn giúp mình nhé.",
        )
    if note is not None and len(note) > 500:
        return ParseIssue(
            "invalid_transaction_field",
            "Mình chưa hiểu rõ ghi chú giao dịch. Bạn nói lại ngắn gọn hơn giúp mình nhé.",
        )
    return None


def _invalid_reasoning_result(message: str) -> ParsedFinanceMessage:
    return ParsedFinanceMessage(
        original_message=message.strip(),
        intent=None,
        amount=None,
        currency=DEFAULT_CURRENCY,
        category=None,
        note=message.strip()[:500],
        occurred_at=datetime.now(VIETNAM_TZ),
        issue=ParseIssue("invalid_llm_output", "Mình chưa hiểu rõ khoản này. Bạn nói rõ loại giao dịch và số tiền giúp mình nhé."),
    )
