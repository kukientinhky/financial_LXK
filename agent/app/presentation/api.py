from __future__ import annotations

from fastapi import FastAPI, Header, HTTPException, status
from fastapi.middleware.cors import CORSMiddleware

from app.application.chat_service import ChatService
from app.config import Settings
from app.domain.finance_parser import VietnameseFinanceParser
from app.infrastructure.backend_api_client import (
    BackendApiClient,
    BackendApiError,
    BackendClientError,
    BackendNetworkError,
    BackendResponseError,
    BackendTimeoutError,
)
from app.infrastructure.mcp_tools import InternalMcpToolClient, InternalMcpToolServer
from app.infrastructure.openrouter_client import OpenRouterFinanceReasoner
from app.presentation.schemas import ChatRequestSchema, ChatResponseSchema


def create_app(settings: Settings | None = None) -> FastAPI:
    resolved_settings = settings or Settings.from_env()
    app = FastAPI(title="ExpenseCraft Agent", version="0.1.0")

    if resolved_settings.cors_origins:
        app.add_middleware(
            CORSMiddleware,
            allow_origins=list(resolved_settings.cors_origins),
            allow_credentials=True,
            allow_methods=["POST", "OPTIONS"],
            allow_headers=["Authorization", "Content-Type"],
        )

    backend_client = BackendApiClient(
        resolved_settings.backend_api_url,
        timeout_seconds=resolved_settings.request_timeout_seconds,
    )
    tool_server = InternalMcpToolServer(backend_client)
    tool_client = InternalMcpToolClient(tool_server)
    reasoner = None
    if resolved_settings.openrouter_api_key:
        reasoner = OpenRouterFinanceReasoner(
            api_key=resolved_settings.openrouter_api_key,
            model=resolved_settings.openrouter_model,
            timeout_seconds=resolved_settings.request_timeout_seconds,
        )
    chat_service = ChatService(
        parser=VietnameseFinanceParser(),
        chat_history=backend_client,
        tools=tool_client,
        reasoner=reasoner,
    )

    @app.post("/api/chat", response_model=ChatResponseSchema)
    async def chat(
        request: ChatRequestSchema,
        authorization: str | None = Header(default=None, alias="Authorization"),
    ) -> ChatResponseSchema:
        bearer = _require_bearer(authorization)
        message = _require_message(request.message)
        try:
            result = await chat_service.handle_message(message, bearer)
        except BackendClientError as exc:
            raise _http_error_from_backend(exc) from exc

        return ChatResponseSchema(
            reply=result.reply,
            intent=result.intent,  # type: ignore[arg-type]
            transaction=dict(result.transaction) if result.transaction is not None else None,
            history_save_failed=result.history_save_failed,
            warning=result.warning,
        )

    return app


def _require_bearer(authorization: str | None) -> str:
    if not authorization or not authorization.strip().lower().startswith("bearer "):
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Authorization Bearer token is required.",
        )
    return authorization.strip()


def _require_message(message: str) -> str:
    trimmed = message.strip()
    if not trimmed:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="Message is required.",
        )
    return trimmed


def _http_error_from_backend(exc: BackendClientError) -> HTTPException:
    if isinstance(exc, BackendApiError):
        if exc.status_code in {status.HTTP_401_UNAUTHORIZED, status.HTTP_403_FORBIDDEN}:
            return HTTPException(status_code=exc.status_code, detail="Backend rejected authorization.")
        return HTTPException(
            status_code=status.HTTP_502_BAD_GATEWAY,
            detail=f"Backend API error ({exc.status_code}).",
        )
    if isinstance(exc, BackendTimeoutError):
        return HTTPException(status_code=status.HTTP_504_GATEWAY_TIMEOUT, detail="Backend API timed out.")
    if isinstance(exc, BackendNetworkError):
        return HTTPException(status_code=status.HTTP_503_SERVICE_UNAVAILABLE, detail="Backend API is unreachable.")
    if isinstance(exc, BackendResponseError):
        return HTTPException(status_code=status.HTTP_502_BAD_GATEWAY, detail="Backend API returned an invalid response.")
    return HTTPException(status_code=status.HTTP_502_BAD_GATEWAY, detail="Backend API error.")
