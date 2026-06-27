from datetime import datetime
from zoneinfo import ZoneInfo

from app.domain.finance_parser import VietnameseFinanceParser
from app.domain.models import FinanceIntent


def test_expense_food_intent_and_category() -> None:
    parsed = VietnameseFinanceParser().parse("tôi vừa chuyển 20k tiền ăn sáng")

    assert parsed.intent == FinanceIntent.EXPENSE
    assert parsed.amount == 20_000
    assert parsed.category == "Ăn uống"
    assert parsed.issue is None


def test_income_salary_intent_and_category() -> None:
    parsed = VietnameseFinanceParser().parse("nhận lương 1,5 triệu")

    assert parsed.intent == FinanceIntent.INCOME
    assert parsed.amount == 1_500_000
    assert parsed.category == "Lương"
    assert parsed.issue is None


def test_transport_category_and_yesterday_date() -> None:
    now = datetime(2026, 6, 27, 9, 30, tzinfo=ZoneInfo("Asia/Ho_Chi_Minh"))
    parsed = VietnameseFinanceParser().parse("hôm qua đổ xăng 50k", now=now)

    assert parsed.intent == FinanceIntent.EXPENSE
    assert parsed.category == "Di chuyển"
    assert parsed.occurred_at.date().isoformat() == "2026-06-26"


def test_transport_category_hint_can_infer_expense() -> None:
    parsed = VietnameseFinanceParser().parse("grab 50k")

    assert parsed.intent == FinanceIntent.EXPENSE
    assert parsed.category == "Di chuyển"


def test_utility_category_hint_can_infer_expense() -> None:
    parsed = VietnameseFinanceParser().parse("tiền điện 300k")

    assert parsed.intent == FinanceIntent.EXPENSE
    assert parsed.category == "Nhà ở/Hóa đơn"


def test_simple_ngay_date() -> None:
    now = datetime(2026, 6, 27, 9, 30, tzinfo=ZoneInfo("Asia/Ho_Chi_Minh"))
    parsed = VietnameseFinanceParser().parse("ngày 05/06 mua áo 200k", now=now)

    assert parsed.intent == FinanceIntent.EXPENSE
    assert parsed.category == "Mua sắm"
    assert parsed.occurred_at.date().isoformat() == "2026-06-05"


def test_bare_simple_date() -> None:
    now = datetime(2026, 6, 27, 9, 30, tzinfo=ZoneInfo("Asia/Ho_Chi_Minh"))
    parsed = VietnameseFinanceParser().parse("05/06 mua áo 200k", now=now)

    assert parsed.intent == FinanceIntent.EXPENSE
    assert parsed.amount == 200_000
    assert parsed.category == "Mua sắm"
    assert parsed.issue is None
    assert parsed.occurred_at.date().isoformat() == "2026-06-05"


def test_invalid_simple_ngay_date_needs_clarification() -> None:
    now = datetime(2026, 6, 27, 9, 30, tzinfo=ZoneInfo("Asia/Ho_Chi_Minh"))
    parsed = VietnameseFinanceParser().parse("ngày 31/02 mua áo 200k", now=now)

    assert parsed.intent == FinanceIntent.EXPENSE
    assert parsed.amount == 200_000
    assert parsed.category == "Mua sắm"
    assert parsed.issue is not None
    assert parsed.issue.code == "invalid_explicit_date"
    assert not parsed.can_create_transaction


def test_invalid_bare_simple_date_needs_clarification() -> None:
    now = datetime(2026, 6, 27, 9, 30, tzinfo=ZoneInfo("Asia/Ho_Chi_Minh"))
    parsed = VietnameseFinanceParser().parse("31/02 mua áo 200k", now=now)

    assert parsed.intent == FinanceIntent.EXPENSE
    assert parsed.amount == 200_000
    assert parsed.category == "Mua sắm"
    assert parsed.issue is not None
    assert parsed.issue.code == "invalid_explicit_date"
    assert not parsed.can_create_transaction


def test_invalid_full_year_ngay_date_needs_clarification() -> None:
    now = datetime(2026, 6, 27, 9, 30, tzinfo=ZoneInfo("Asia/Ho_Chi_Minh"))
    parsed = VietnameseFinanceParser().parse("ngày 31/02/2026 mua áo 200k", now=now)

    assert parsed.intent == FinanceIntent.EXPENSE
    assert parsed.amount == 200_000
    assert parsed.category == "Mua sắm"
    assert parsed.issue is not None
    assert parsed.issue.code == "invalid_explicit_date"
    assert not parsed.can_create_transaction


def test_message_without_explicit_date_defaults_to_now() -> None:
    now = datetime(2026, 6, 27, 9, 30, tzinfo=ZoneInfo("Asia/Ho_Chi_Minh"))
    parsed = VietnameseFinanceParser().parse("mua áo 200k", now=now)

    assert parsed.issue is None
    assert parsed.can_create_transaction
    assert parsed.occurred_at == now


def test_mixed_income_expense_signals_are_ambiguous() -> None:
    parsed = VietnameseFinanceParser().parse("thu chi 20k")

    assert parsed.intent is None
    assert parsed.amount == 20_000
    assert parsed.category is None
    assert parsed.issue is not None
    assert parsed.issue.code == "ambiguous_intent"
    assert not parsed.can_create_transaction


def test_low_confidence_intent_does_not_create_transaction() -> None:
    parsed = VietnameseFinanceParser().parse("chuyển 20k")

    assert parsed.intent is None
    assert parsed.amount == 20_000
    assert parsed.issue is not None
    assert parsed.issue.code == "low_confidence_intent"
    assert not parsed.can_create_transaction
