from __future__ import annotations

import os
from dataclasses import dataclass


DEFAULT_BACKEND_API_URL = "http://localhost:5000"
DEFAULT_CORS_ORIGINS = "http://localhost:3000,http://127.0.0.1:3000"
DEFAULT_OPENROUTER_MODEL = "openai/gpt-4o-mini"


@dataclass(frozen=True)
class Settings:
    backend_api_url: str = DEFAULT_BACKEND_API_URL
    request_timeout_seconds: float = 10.0
    cors_origins: tuple[str, ...] = ("http://localhost:3000", "http://127.0.0.1:3000")
    openrouter_api_key: str | None = None
    openrouter_model: str = DEFAULT_OPENROUTER_MODEL

    @classmethod
    def from_env(cls) -> "Settings":
        return cls(
            backend_api_url=os.getenv("BACKEND_API_URL", DEFAULT_BACKEND_API_URL).rstrip("/"),
            request_timeout_seconds=_parse_float(
                os.getenv("AGENT_REQUEST_TIMEOUT_SECONDS"),
                default=10.0,
            ),
            cors_origins=_parse_csv(
                os.getenv("AGENT_CORS_ORIGINS", DEFAULT_CORS_ORIGINS),
            ),
            openrouter_api_key=_blank_to_none(os.getenv("OPENROUTER_API_KEY")),
            openrouter_model=os.getenv("OPENROUTER_MODEL", DEFAULT_OPENROUTER_MODEL).strip()
            or DEFAULT_OPENROUTER_MODEL,
        )


def _parse_float(value: str | None, default: float) -> float:
    if not value:
        return default

    try:
        parsed = float(value)
    except ValueError:
        return default

    return parsed if parsed > 0 else default


def _parse_csv(value: str) -> tuple[str, ...]:
    return tuple(item.strip() for item in value.split(",") if item.strip())


def _blank_to_none(value: str | None) -> str | None:
    if value is None or not value.strip():
        return None
    return value.strip()
