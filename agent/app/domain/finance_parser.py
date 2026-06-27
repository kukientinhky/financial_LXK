from __future__ import annotations

import re
import unicodedata
from dataclasses import dataclass
from datetime import datetime, timedelta
from decimal import Decimal, InvalidOperation, ROUND_HALF_UP
from zoneinfo import ZoneInfo

from app.domain.models import FinanceIntent, ParsedFinanceMessage, ParseIssue


VIETNAM_TZ = ZoneInfo("Asia/Ho_Chi_Minh")
DEFAULT_CURRENCY = "VND"
MIN_CONFIDENT_INTENT_SCORE = 2

THOUSAND_UNITS = {"k", "nghin", "ngan"}
MILLION_UNITS = {"tr", "trieu", "m"}
AMOUNT_RE = re.compile(
    r"(?<!\w)(?P<number>\d+(?:(?:[.,]\d+)+)?)(?:\s*(?P<unit>k|nghin|ngan|trieu|tr|m))?(?:\s*(?:vnd|dong|d))?(?!\w)",
    re.IGNORECASE,
)
GROUPED_INTEGER_RE = re.compile(r"^\d{1,3}(?:[.,]\d{3})+$")
DATE_RE = re.compile(
    r"(?<![\w/.-])(?:ngay\s+)?(?P<day>\d{1,2})[/-](?P<month>\d{1,2})(?:[/-](?P<year>\d{2,4}))?(?![\w/-])"
)


@dataclass(frozen=True)
class _IntentResolution:
    intent: FinanceIntent | None
    issue_code: str | None = None


@dataclass(frozen=True)
class _OccurredAtResolution:
    occurred_at: datetime
    issue_code: str | None = None


EXPENSE_TERMS: tuple[tuple[str, int], ...] = (
    ("thanh toan", 3),
    ("do xang", 3),
    ("chi", 3),
    ("tra", 2),
    ("mua", 2),
    ("ton", 2),
    ("dong", 2),
    ("chuyen", 1),
    ("an", 2),
    ("uong", 2),
    ("grab", 2),
    ("taxi", 2),
    ("bus", 2),
    ("xe buyt", 2),
    ("xang", 2),
    ("internet", 2),
    ("wifi", 2),
    ("hoa don", 2),
    ("thue nha", 2),
    ("tien nha", 2),
    ("tien dien", 2),
    ("tien nuoc", 2),
    ("dien nuoc", 2),
    ("sieu thi", 2),
    ("cafe", 2),
    ("ca phe", 2),
    ("tra sua", 2),
    ("com", 2),
    ("pho", 2),
    ("bun", 2),
    ("banh", 2),
)

INCOME_TERMS: tuple[tuple[str, int], ...] = (
    ("hoan tien", 3),
    ("tien vao", 3),
    ("luong", 3),
    ("thuong", 3),
    ("nhan", 2),
    ("ban", 2),
    ("thu", 2),
    ("duoc", 1),
)

FOOD_TERMS = (
    "an uong",
    "an",
    "uong",
    "com",
    "pho",
    "bun",
    "banh",
    "cafe",
    "ca phe",
    "tra sua",
    "do an",
    "do uong",
    "bua sang",
    "an sang",
    "an trua",
    "an toi",
)
TRANSPORT_TERMS = (
    "di chuyen",
    "do xang",
    "xang",
    "grab",
    "taxi",
    "bus",
    "xe buyt",
    "xe om",
    "ve xe",
    "gui xe",
)
UTILITY_TERMS = (
    "hoa don",
    "thue nha",
    "tien nha",
    "tien dien",
    "tien nuoc",
    "dien nuoc",
    "nha tro",
    "internet",
    "wifi",
)
SHOPPING_TERMS = (
    "mua sam",
    "sieu thi",
    "quan ao",
    "dien thoai",
    "shopping",
    "tap hoa",
    "ao",
    "quan",
    "giay",
    "dep",
)


class VietnameseFinanceParser:
    """Deterministic parser for common Vietnamese finance messages."""

    def parse(self, message: str, now: datetime | None = None) -> ParsedFinanceMessage:
        current_time = _coerce_now(now)
        original = message.strip()
        normalized = normalize_text(original)

        amount = parse_amount(original)
        intent_resolution = _resolve_intent(normalized)
        intent = intent_resolution.intent
        occurred_at_resolution = _parse_occurred_at(normalized, current_time)
        issue = _build_issue(amount, intent_resolution, occurred_at_resolution.issue_code)
        category = _map_category(normalized, intent) if intent else None

        return ParsedFinanceMessage(
            original_message=original,
            intent=intent,
            amount=amount,
            currency=DEFAULT_CURRENCY,
            category=category,
            note=_truncate(original, 500),
            occurred_at=occurred_at_resolution.occurred_at,
            issue=issue,
        )


def parse_amount(message: str) -> int | None:
    normalized = normalize_text(message)
    candidates: list[tuple[int, int, int]] = []

    for match in AMOUNT_RE.finditer(normalized):
        if _touches_date_separator(normalized, match.start(), match.end()):
            continue

        number_text = match.group("number")
        unit = match.group("unit")
        unit = unit.lower() if unit else None
        amount = _amount_from_match(number_text, unit)
        if amount is None or amount <= 0:
            continue
        if unit is None and not GROUPED_INTEGER_RE.match(number_text) and amount < 1_000:
            continue

        score = _amount_candidate_score(number_text, unit, amount)
        candidates.append((score, match.start(), amount))

    if not candidates:
        return None

    candidates.sort(key=lambda item: (-item[0], item[1]))
    return candidates[0][2]


def normalize_text(value: str) -> str:
    lowered = value.lower().replace("đ", "d")
    decomposed = unicodedata.normalize("NFD", lowered)
    stripped = "".join(character for character in decomposed if unicodedata.category(character) != "Mn")
    return re.sub(r"\s+", " ", stripped).strip()


def _coerce_now(now: datetime | None) -> datetime:
    if now is None:
        return datetime.now(VIETNAM_TZ)
    if now.tzinfo is None:
        return now.replace(tzinfo=VIETNAM_TZ)
    return now.astimezone(VIETNAM_TZ)


def _touches_date_separator(text: str, start: int, end: int) -> bool:
    return (start > 0 and text[start - 1] in "/-") or (end < len(text) and text[end] in "/-")


def _amount_from_match(number_text: str, unit: str | None) -> int | None:
    multiplier = Decimal(1)
    if unit in THOUSAND_UNITS:
        multiplier = Decimal(1_000)
    elif unit in MILLION_UNITS:
        multiplier = Decimal(1_000_000)

    try:
        number = _parse_decimal_number(number_text, unit)
    except InvalidOperation:
        return None

    amount = (number * multiplier).quantize(Decimal("1"), rounding=ROUND_HALF_UP)
    return int(amount)


def _parse_decimal_number(number_text: str, unit: str | None) -> Decimal:
    if GROUPED_INTEGER_RE.match(number_text):
        return Decimal(number_text.replace(".", "").replace(",", ""))

    decimal_text = number_text.replace(",", ".")
    if unit is None and decimal_text.count(".") > 1:
        decimal_text = decimal_text.replace(".", "")

    return Decimal(decimal_text)


def _amount_candidate_score(number_text: str, unit: str | None, amount: int) -> int:
    if unit:
        return 100
    if GROUPED_INTEGER_RE.match(number_text):
        return 80
    if amount >= 1_000:
        return 60
    return 1


def _resolve_intent(normalized: str) -> _IntentResolution:
    expense_score = _score_terms(normalized, EXPENSE_TERMS)
    income_score = _score_terms(normalized, INCOME_TERMS)

    if expense_score > 0 and income_score > 0:
        return _IntentResolution(intent=None, issue_code="ambiguous_intent")
    if expense_score == 0 and income_score == 0:
        return _IntentResolution(intent=None, issue_code="missing_intent")
    if expense_score >= MIN_CONFIDENT_INTENT_SCORE:
        return _IntentResolution(intent=FinanceIntent.EXPENSE)
    if income_score >= MIN_CONFIDENT_INTENT_SCORE:
        return _IntentResolution(intent=FinanceIntent.INCOME)
    return _IntentResolution(intent=None, issue_code="low_confidence_intent")


def _score_terms(normalized: str, weighted_terms: tuple[tuple[str, int], ...]) -> int:
    return sum(weight for term, weight in weighted_terms if _contains_term(normalized, term))


def _contains_any(normalized: str, terms: tuple[str, ...]) -> bool:
    return any(_contains_term(normalized, term) for term in terms)


def _contains_term(normalized: str, term: str) -> bool:
    return re.search(rf"(?<!\w){re.escape(term)}(?!\w)", normalized) is not None


def _map_category(normalized: str, intent: FinanceIntent | None) -> str | None:
    if intent == FinanceIntent.INCOME:
        if _contains_term(normalized, "luong"):
            return "Lương"
        if _contains_term(normalized, "thuong"):
            return "Thưởng"
        return "Thu nhập khác"

    if intent == FinanceIntent.EXPENSE:
        if _contains_any(normalized, FOOD_TERMS):
            return "Ăn uống"
        if _contains_any(normalized, TRANSPORT_TERMS):
            return "Di chuyển"
        if _contains_any(normalized, UTILITY_TERMS):
            return "Nhà ở/Hóa đơn"
        if _contains_any(normalized, SHOPPING_TERMS):
            return "Mua sắm"
        return "Khác"

    return None


def _parse_occurred_at(normalized: str, now: datetime) -> _OccurredAtResolution:
    date_match = DATE_RE.search(normalized)
    if date_match:
        parsed = _date_from_match(date_match, now)
        if parsed is not None:
            return _OccurredAtResolution(parsed)
        return _OccurredAtResolution(now, "invalid_explicit_date")

    if _contains_term(normalized, "hom qua"):
        return _OccurredAtResolution(now - timedelta(days=1))

    return _OccurredAtResolution(now)


def _date_from_match(match: re.Match[str], now: datetime) -> datetime | None:
    day = int(match.group("day"))
    month = int(match.group("month"))
    raw_year = match.group("year")
    if raw_year is None:
        year = now.year
    else:
        year = int(raw_year)
        if year < 100:
            year += 2000

    try:
        return now.replace(year=year, month=month, day=day)
    except ValueError:
        return None


def _build_issue(
    amount: int | None,
    intent_resolution: _IntentResolution,
    date_issue_code: str | None = None,
) -> ParseIssue | None:
    intent = intent_resolution.intent
    intent_issue = intent_resolution.issue_code

    if date_issue_code == "invalid_explicit_date":
        return ParseIssue("invalid_explicit_date", "Explicit transaction date is invalid.")

    if amount is None and intent is None:
        if intent_issue == "ambiguous_intent":
            return ParseIssue("ambiguous_intent_and_missing_amount", "Intent is ambiguous and amount is missing.")
        if intent_issue == "low_confidence_intent":
            return ParseIssue(
                "low_confidence_intent_and_missing_amount",
                "Intent confidence is too low and amount is missing.",
            )
        return ParseIssue("missing_intent_and_amount", "Intent and amount are missing.")
    if amount is None:
        return ParseIssue("missing_amount", "Amount is missing.")
    if intent is None:
        code = intent_issue or "missing_intent"
        if code == "ambiguous_intent":
            message = "Intent is ambiguous."
        elif code == "low_confidence_intent":
            message = "Intent confidence is too low."
        else:
            message = "Intent is missing."
        return ParseIssue(code, message)
    return None


def _truncate(value: str, max_length: int) -> str:
    return value if len(value) <= max_length else value[:max_length]
