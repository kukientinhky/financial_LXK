from app.domain.finance_parser import parse_amount


def test_parse_thousand_units() -> None:
    assert parse_amount("chi ăn sáng 20k") == 20_000
    assert parse_amount("trả 20 nghìn tiền gửi xe") == 20_000
    assert parse_amount("uống cà phê 20 ngàn") == 20_000


def test_parse_million_units_with_decimal_separator() -> None:
    assert parse_amount("nhận 1.5tr tiền thưởng") == 1_500_000
    assert parse_amount("lương 1,5 triệu") == 1_500_000


def test_parse_plain_vnd_numbers() -> None:
    assert parse_amount("mua đồ 20000") == 20_000
    assert parse_amount("thanh toán 1.500.000 tiền nhà") == 1_500_000


def test_small_plain_counts_are_not_treated_as_amounts() -> None:
    assert parse_amount("mua 2 cái áo") is None
