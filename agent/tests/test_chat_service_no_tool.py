from __future__ import annotations

from typing import Any, Mapping

import pytest

from app.application.chat_service import ChatService
from app.application.dto import ChatMessageToSave, FinanceReasoningResult, TransactionToolRequest
from app.application.interfaces.llm_reasoner import FinanceReasoningError
from app.domain.finance_parser import VietnameseFinanceParser


class FakeChatHistory:
    def __init__(self) -> None:
        self.messages: list[ChatMessageToSave] = []

    async def save_message(self, authorization: str, message: ChatMessageToSave) -> Mapping[str, Any]:
        self.messages.append(message)
        return {"id": f"message-{len(self.messages)}"}


class FailingAssistantHistory(FakeChatHistory):
    async def save_message(self, authorization: str, message: ChatMessageToSave) -> Mapping[str, Any]:
        if message.role == "assistant":
            raise RuntimeError("history persistence failed")
        return await super().save_message(authorization, message)


class RecordingTools:
    def __init__(self) -> None:
        self.calls: list[str] = []

    async def income(self, authorization: str, transaction: TransactionToolRequest) -> Mapping[str, Any]:
        self.calls.append("income")
        return {"id": "income-1"}

    async def expense(self, authorization: str, transaction: TransactionToolRequest) -> Mapping[str, Any]:
        self.calls.append("expense")
        return {"id": "expense-1"}


class FakeReasoner:
    def __init__(self, result: FinanceReasoningResult | Exception) -> None:
        self.result = result

    async def reason(self, message: str) -> FinanceReasoningResult:
        if isinstance(self.result, Exception):
            raise self.result
        return self.result


@pytest.mark.asyncio
async def test_missing_amount_does_not_call_tools_and_saves_reply() -> None:
    history = FakeChatHistory()
    tools = RecordingTools()
    service = ChatService(VietnameseFinanceParser(), history, tools)

    result = await service.handle_message("chi tiền ăn", "Bearer test-token")

    assert tools.calls == []
    assert result.transaction is None
    assert result.intent == "expense"
    assert "số tiền" in result.reply
    assert [message.role for message in history.messages] == ["user", "assistant"]
    assert history.messages[0].intent == "expense"


@pytest.mark.asyncio
async def test_mixed_intent_does_not_call_tools_and_asks_for_clarification() -> None:
    history = FakeChatHistory()
    tools = RecordingTools()
    service = ChatService(VietnameseFinanceParser(), history, tools)

    result = await service.handle_message("thu chi 20k", "Bearer test-token")

    assert tools.calls == []
    assert result.transaction is None
    assert result.intent is None
    assert "thu hay chi" in result.reply
    assert [message.role for message in history.messages] == ["user", "assistant"]


@pytest.mark.asyncio
async def test_invalid_explicit_date_does_not_call_tools_and_asks_for_clarification() -> None:
    history = FakeChatHistory()
    tools = RecordingTools()
    service = ChatService(VietnameseFinanceParser(), history, tools)

    result = await service.handle_message("ngày 31/02 mua áo 200k", "Bearer test-token")

    assert tools.calls == []
    assert result.transaction is None
    assert result.intent == "expense"
    assert "Ngày giao dịch không hợp lệ" in result.reply
    assert [message.role for message in history.messages] == ["user", "assistant"]
    assert history.messages[0].intent == "expense"


@pytest.mark.asyncio
async def test_invalid_bare_explicit_date_does_not_call_tools_and_asks_for_clarification() -> None:
    history = FakeChatHistory()
    tools = RecordingTools()
    service = ChatService(VietnameseFinanceParser(), history, tools)

    result = await service.handle_message("31/02 mua áo 200k", "Bearer test-token")

    assert tools.calls == []
    assert result.transaction is None
    assert result.intent == "expense"
    assert "Ngày giao dịch không hợp lệ" in result.reply
    assert [message.role for message in history.messages] == ["user", "assistant"]
    assert history.messages[0].intent == "expense"


@pytest.mark.asyncio
async def test_committed_transaction_returns_success_when_assistant_history_save_fails() -> None:
    history = FailingAssistantHistory()
    tools = RecordingTools()
    service = ChatService(VietnameseFinanceParser(), history, tools)

    result = await service.handle_message("chi ăn sáng 20k", "Bearer test-token")

    assert tools.calls == ["expense"]
    assert result.transaction == {"id": "expense-1"}
    assert result.intent == "expense"
    assert result.history_save_failed is True
    assert result.warning is not None
    assert "không lưu được" in result.warning
    assert len(history.messages) == 1
    assert history.messages[0].role == "user"


@pytest.mark.asyncio
async def test_llm_income_message_calls_income_tool() -> None:
    history = FakeChatHistory()
    tools = RecordingTools()
    service = ChatService(
        VietnameseFinanceParser(),
        history,
        tools,
        reasoner=FakeReasoner(
            FinanceReasoningResult(
                original_message="nhận lương 5 triệu",
                intent="income",
                amount=5_000_000,
                currency="VND",
                category="Lương",
                note="nhận lương",
                occurred_at=None,
            )
        ),
    )

    result = await service.handle_message("nhận lương 5 triệu", "Bearer test-token")

    assert tools.calls == ["income"]
    assert result.intent == "income"
    assert result.transaction == {"id": "income-1"}
    assert history.messages[0].intent == "income"


@pytest.mark.asyncio
async def test_llm_expense_message_calls_expense_tool() -> None:
    history = FakeChatHistory()
    tools = RecordingTools()
    service = ChatService(
        VietnameseFinanceParser(),
        history,
        tools,
        reasoner=FakeReasoner(
            FinanceReasoningResult(
                original_message="ăn sáng 20k",
                intent="expense",
                amount=20_000,
                currency="VND",
                category="Ăn uống",
                note="ăn sáng",
                occurred_at=None,
            )
        ),
    )

    result = await service.handle_message("ăn sáng 20k", "Bearer test-token")

    assert tools.calls == ["expense"]
    assert result.intent == "expense"
    assert result.transaction == {"id": "expense-1"}
    assert history.messages[0].intent == "expense"


@pytest.mark.asyncio
async def test_llm_unknown_missing_info_asks_clarification_and_no_tool_call() -> None:
    history = FakeChatHistory()
    tools = RecordingTools()
    service = ChatService(
        VietnameseFinanceParser(),
        history,
        tools,
        reasoner=FakeReasoner(
            FinanceReasoningResult(
                original_message="ghi tiền ăn",
                intent="unknown",
                amount=None,
                currency="VND",
                category=None,
                note=None,
                occurred_at=None,
                clarification="Bạn muốn ghi thu hay chi và số tiền là bao nhiêu?",
            )
        ),
    )

    result = await service.handle_message("ghi tiền ăn", "Bearer test-token")

    assert tools.calls == []
    assert result.intent is None
    assert result.transaction is None
    assert "số tiền" in result.reply
    assert history.messages[0].intent == "unknown"


@pytest.mark.asyncio
async def test_invalid_malformed_llm_output_no_tool_call() -> None:
    history = FakeChatHistory()
    tools = RecordingTools()
    service = ChatService(
        VietnameseFinanceParser(),
        history,
        tools,
        reasoner=FakeReasoner(FinanceReasoningError("malformed")),
    )

    result = await service.handle_message("ăn sáng 20k", "Bearer test-token")

    assert tools.calls == []
    assert result.intent is None
    assert result.transaction is None
    assert history.messages[0].intent == "unknown"


@pytest.mark.asyncio
async def test_llm_mixed_intent_safety_gate_blocks_tool_call() -> None:
    history = FakeChatHistory()
    tools = RecordingTools()
    service = ChatService(
        VietnameseFinanceParser(),
        history,
        tools,
        reasoner=FakeReasoner(
            FinanceReasoningResult(
                original_message="thu chi 20k",
                intent="expense",
                amount=20_000,
                currency="VND",
                category="Khác",
                note="thu chi 20k",
                occurred_at=None,
            )
        ),
    )

    result = await service.handle_message("thu chi 20k", "Bearer test-token")

    assert tools.calls == []
    assert result.transaction is None
    assert result.intent is None
    assert "thu hay chi" in result.reply
    assert [message.role for message in history.messages] == ["user", "assistant"]


@pytest.mark.asyncio
async def test_llm_overlong_category_blocks_tool_call() -> None:
    history = FakeChatHistory()
    tools = RecordingTools()
    service = ChatService(
        VietnameseFinanceParser(),
        history,
        tools,
        reasoner=FakeReasoner(
            FinanceReasoningResult(
                original_message="ăn sáng 20k",
                intent="expense",
                amount=20_000,
                currency="VND",
                category="a" * 101,
                note="ăn sáng",
                occurred_at=None,
            )
        ),
    )

    result = await service.handle_message("ăn sáng 20k", "Bearer test-token")

    assert tools.calls == []
    assert result.transaction is None
    assert result.intent == "expense"
    assert "nhóm" in result.reply


@pytest.mark.asyncio
async def test_llm_overlong_note_blocks_tool_call() -> None:
    history = FakeChatHistory()
    tools = RecordingTools()
    service = ChatService(
        VietnameseFinanceParser(),
        history,
        tools,
        reasoner=FakeReasoner(
            FinanceReasoningResult(
                original_message="ăn sáng 20k",
                intent="expense",
                amount=20_000,
                currency="VND",
                category="Ăn uống",
                note="a" * 501,
                occurred_at=None,
            )
        ),
    )

    result = await service.handle_message("ăn sáng 20k", "Bearer test-token")

    assert tools.calls == []
    assert result.transaction is None
    assert result.intent == "expense"
    assert "ghi chú" in result.reply
