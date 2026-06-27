# Python/MCP Chatbot Flow

The agent is a FastAPI service that exposes `POST /api/chat` and coordinates persisted chat history, OpenRouter LLM reasoning, deterministic Vietnamese fallback parsing, and MCP-like write tools.

## Runtime flow

```text
Frontend dashboard
  -> POST {AGENT_URL}/api/chat with Authorization: Bearer <token>
Agent /api/chat
  -> validates bearer token shape and non-empty message
  -> parses Vietnamese finance intent (`income`, `expense`, or `unknown`)
  -> saves user message through .NET POST /api/chat/messages
  -> if intent + amount are complete:
       calls internal MCP-like tool income or expense
       tool forwards the same JWT to .NET POST /api/agent/tools/income|expense
       .NET writes the transaction for the authenticated user
     else:
       no tool call; assistant asks for clarification
  -> saves assistant message through .NET POST /api/chat/messages
  -> returns reply, intent, and optional transaction to frontend
```

JWT is forwarded unchanged from frontend to agent to backend. The backend still owns authorization and derives transaction/chat ownership from the JWT.

## Agent endpoint

```http
POST /api/chat
Authorization: Bearer <accessToken>
Content-Type: application/json
```

Request:

```json
{ "message": "tôi vừa chuyển 20k tiền ăn sáng" }
```

Response when a transaction is created:

```json
{
  "reply": "Đã ghi khoản chi 20.000₫ cho Ăn uống.",
  "intent": "expense",
  "transaction": {
    "id": "4a3382bc-f19a-4c29-8b90-2e63b7d7e3f4",
    "type": "expense",
    "amount": 20000,
    "currency": "VND",
    "category": "Ăn uống",
    "note": "tôi vừa chuyển 20k tiền ăn sáng",
    "source": "agent",
    "occurredAt": "2026-06-27T02:30:00Z",
    "createdAt": "2026-06-27T02:31:00Z"
  },
  "history_save_failed": false,
  "warning": null
}
```

If the transaction write succeeds but persisting the assistant reply to chat history fails, the agent still returns success because the money movement was already committed:

```json
{
  "reply": "Đã ghi khoản chi 20.000₫ cho Ăn uống.",
  "intent": "expense",
  "transaction": {
    "id": "4a3382bc-f19a-4c29-8b90-2e63b7d7e3f4",
    "type": "expense",
    "amount": 20000,
    "currency": "VND"
  },
  "history_save_failed": true,
  "warning": "Giao dịch đã được ghi nhưng không lưu được phản hồi vào lịch sử."
}
```

Clients should not blindly retry this request: retrying can create a duplicate transaction. Refresh transactions/chat history and surface the warning instead.

Response when clarification is needed:

```json
{
  "reply": "Bạn cho mình biết số tiền cần ghi nhận là bao nhiêu nhé.",
  "intent": "expense",
  "transaction": null,
  "history_save_failed": false,
  "warning": null
}
```

Agent error cases:

- `401` when `Authorization: Bearer <token>` is missing or backend rejects authorization.
- `400` when `message` is blank.
- `422` when request body validation fails, for example message length exceeds 4000 characters.
- `503` when the backend API is unreachable.
- `504` when the backend API times out.
- `502` when the backend API returns another non-success response.

## MCP-like tools

The internal tool server exposes exactly two write tools. Public backend tool API routes are English only:

- `income` -> backend `POST /api/agent/tools/income` -> income transaction.
- `expense` -> backend `POST /api/agent/tools/expense` -> expense transaction.

Tool request fields sent to .NET:

```json
{
  "amount": 20000,
  "currency": "VND",
  "category": "Ăn uống",
  "note": "tôi vừa chuyển 20k tiền ăn sáng",
  "source": "agent",
  "occurredAt": "2026-06-27T09:30:00+07:00"
}
```

Phase 1 writes VND only. The backend defaults blank currency to `VND`, rejects non-`VND`, rounds amounts to 2 decimals, and derives ownership from the forwarded JWT. See [authenticated backend APIs](../backend/authenticated-apis.md) for backend validation and ownership rules.

## Reasoning and Vietnamese parser examples

When `OPENROUTER_API_KEY` is set, the primary path is OpenRouter Chat Completions reasoning with structured JSON output. The agent validates the returned intent, amount, currency, category, note, and date before any write. If no API key is configured, local/test fallback parsing is deterministic: it normalizes Vietnamese text, detects amount units (`k`, `nghìn`, `ngàn`, `tr`, `triệu`, `m`), maps common categories, and supports `hôm qua` plus explicit dates as `ngày dd/mm[/yyyy]` or bare `dd/mm[/yyyy]` in the `Asia/Ho_Chi_Minh` timezone. Impossible explicit dates in either form ask for clarification and do not call write tools.

Do not commit OpenRouter API keys. If a key was pasted into chat, logs, docs, source, or git history, rotate/revoke it immediately before continuing to use the agent.

| Message | Intent/tool | Structured transaction fields |
| --- | --- | --- |
| `tôi vừa chuyển 20k tiền ăn sáng` | `expense` -> `expense` tool | `amount=20000`, `currency=VND`, `category=Ăn uống`, `note=<original>`, `source=agent`, `occurredAt=now` |
| `nhận lương 1,5 triệu` | `income` -> `income` tool | `amount=1500000`, `currency=VND`, `category=Lương`, `note=<original>`, `source=agent` |
| `hôm qua đổ xăng 50k` | `expense` -> `expense` tool | `amount=50000`, `category=Di chuyển`, `occurredAt=yesterday` |
| `ngày 05/06 mua áo 200k` | `expense` -> `expense` tool | `amount=200000`, `category=Mua sắm`, `occurredAt=current-year-06-05` |
| `05/06 mua áo 200k` | `expense` -> `expense` tool | `amount=200000`, `category=Mua sắm`, `occurredAt=current-year-06-05` |
| `ngày 31/02/2026 mua áo 200k` | no tool | Invalid explicit date; assistant asks `Ngày giao dịch không hợp lệ. Bạn cho mình biết lại ngày theo định dạng dd/mm hoặc dd/mm/yyyy nhé.` |
| `31/02/2026 mua áo 200k` | no tool | Invalid bare explicit date; assistant asks the same clarification and does not call `expense`/`income`. |
| `chi tiền ăn` | no tool | Missing amount; assistant asks `Bạn cho mình biết số tiền cần ghi nhận là bao nhiêu nhé.` |
| `thu chi 20k` | no tool | Ambiguous mixed income/expense intent; assistant asks `Bạn muốn ghi khoản này là thu hay chi?` |
