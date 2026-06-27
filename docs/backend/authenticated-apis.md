# Authenticated Backend APIs

All endpoints in this file are under the .NET backend and require:

```http
Authorization: Bearer <accessToken>
```

The backend derives `userId` from the JWT `NameIdentifier` claim populated at login. Clients must not send `userId`; reads, writes, deletes, analytics, and chat history are scoped to the authenticated owner.

Application/domain validation failures are mapped to Problem Details responses, usually with `400 Bad Request` and a clear `detail`. Ownership failures for linked resources, such as saving a chat message with another user's `transactionId`, return `404 Not Found`. ASP.NET route/body binding errors still use the framework response for the current environment.

Example validation response:

```json
{
  "title": "Invalid request",
  "status": 400,
  "detail": "Only VND currency is supported."
}
```

## Transaction rules

- `amount` is rounded to 2 decimals using away-from-zero rounding, then must be positive.
- Phase 1 supports VND only. `currency` defaults to `VND` when omitted or blank, is trimmed/uppercased, must be at most 10 characters, and returns `400 Bad Request` for any non-`VND` value.
- `category` is required, trimmed, and must be at most 100 characters.
- `note` is optional and must be at most 500 characters.
- `source` is optional and must be at most 100 characters; agent tool endpoints default it to `agent`.
- `occurredAt` is optional; when supplied, offsets such as `+07:00` are accepted and stored as UTC. When omitted, the backend uses current UTC time.
- Deleted transactions are soft-deleted and excluded from transaction lists and analytics.

Common transaction response:

```json
{
  "id": "4a3382bc-f19a-4c29-8b90-2e63b7d7e3f4",
  "type": "expense",
  "amount": 20000,
  "currency": "VND",
  "category": "Ăn uống",
  "note": "tôi vừa chuyển 20k tiền ăn sáng",
  "source": "agent",
  "occurredAt": "2026-06-27T02:30:00Z",
  "createdAt": "2026-06-27T02:31:00Z"
}
```

## `POST /api/agent/tools/income`

Creates an income transaction for the current user. The transaction `type` is fixed to `income` by the route.

Request body:

```json
{
  "amount": 1500000,
  "currency": "VND",
  "category": "Lương",
  "note": "nhận lương 1,5 triệu",
  "source": "agent",
  "occurredAt": "2026-06-27T09:30:00+07:00"
}
```

Success: `201 Created` with the transaction response and `Location: /api/transactions/{id}`.

Error cases:

- `401 Unauthorized` when the bearer token is missing, invalid, or expired.
- `400 Bad Request` when amount, category, currency, note, source, or date values violate the transaction rules above, for example non-positive amount or non-`VND` currency. Domain/application validation uses Problem Details; malformed JSON/date binding uses ASP.NET's framework response.

## `POST /api/agent/tools/expense`

Creates an expense transaction for the current user. The transaction `type` is fixed to `expense` by the route.

Request body:

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

Success: `201 Created` with the transaction response and `Location: /api/transactions/{id}`.

Error cases are the same as `income`.

## `GET /api/transactions?limit=20`

Returns recent active transactions owned by the current user, ordered by `occurredAt` then `createdAt` descending.

Query:

- `limit` optional, defaults to `20`, clamped to `1..100`.

Success: `200 OK`

```json
[
  {
    "id": "4a3382bc-f19a-4c29-8b90-2e63b7d7e3f4",
    "type": "expense",
    "amount": 20000,
    "currency": "VND",
    "category": "Ăn uống",
    "note": "tôi vừa chuyển 20k tiền ăn sáng",
    "source": "agent",
    "occurredAt": "2026-06-27T02:30:00Z",
    "createdAt": "2026-06-27T02:31:00Z"
  }
]
```

Error cases:

- `401 Unauthorized` for missing, invalid, or expired bearer token.

## `DELETE /api/transactions/{id}`

Soft-deletes an active transaction owned by the current user.

Success:

- `204 No Content` when the transaction exists, is active, and belongs to the current user.

Error cases:

- `401 Unauthorized` for missing, invalid, or expired bearer token.
- `404 Not Found` when the id does not exist, belongs to another user, or was already deleted.
- `400 Bad Request` Problem Details when `{id}` is the empty GUID (`00000000-0000-0000-0000-000000000000`).
- `404 Not Found` when `{id}` is not a GUID because the `{id:guid}` route constraint does not match.

## `GET /api/analytics/summary?from&to`

Returns database-backed analytics for active transactions owned by the current user.

Query:

- `from`: required `YYYY-MM-DD` date, inclusive in `Asia/Ho_Chi_Minh` local time.
- `to`: required `YYYY-MM-DD` date, inclusive in `Asia/Ho_Chi_Minh` local time; must be greater than or equal to `from`.

Transactions are selected from local midnight at `from` to before local midnight after `to`, converted to UTC for storage queries. Monthly cashflow groups also use `Asia/Ho_Chi_Minh` local year/month, so a UTC timestamp near midnight is reported in the month that Vietnamese users expect.

Success: `200 OK`

```json
{
  "totalIncome": 1500000,
  "totalExpense": 70000,
  "net": 1430000,
  "currency": "VND",
  "cashflowByMonth": [
    { "month": "2026-06", "income": 1500000, "expense": 70000, "net": 1430000 }
  ],
  "expenseByCategory": [
    { "category": "Ăn uống", "amount": 20000, "percent": 28.57 }
  ],
  "reasonableness": {
    "status": "healthy",
    "message": "Expense level is healthy relative to income.",
    "expenseRatio": 0.0467,
    "categoryWarnings": []
  }
}
```

`reasonableness.status` is one of `healthy`, `watch`, `high`, or `insufficient_data`.

Error cases:

- `401 Unauthorized` for missing, invalid, or expired bearer token.
- `400 Bad Request` for missing/malformed `from` or `to`, `to < from`, or unsupported non-VND historical data found in the selected range.

## `GET /api/chat/messages?limit=50`

Returns recent persisted chat messages owned by the current user. The repository fetches newest messages first, then returns them oldest-to-newest for display.

Query:

- `limit` optional, defaults to `50`, clamped to `1..200`.

Success: `200 OK`

```json
[
  {
    "id": "6aa61c3c-12f6-4830-a12e-30f519f34303",
    "role": "user",
    "content": "tôi vừa chuyển 20k tiền ăn sáng",
    "intent": "expense",
    "transactionId": null,
    "createdAt": "2026-06-27T02:30:00Z"
  },
  {
    "id": "c6dd8a2f-02d2-4f26-b8d4-8621c58bd2a8",
    "role": "assistant",
    "content": "Đã ghi khoản chi 20.000₫ cho Ăn uống.",
    "intent": "expense",
    "transactionId": "4a3382bc-f19a-4c29-8b90-2e63b7d7e3f4",
    "createdAt": "2026-06-27T02:31:00Z"
  }
]
```

Error cases:

- `401 Unauthorized` for missing, invalid, or expired bearer token.

## `POST /api/chat/messages`

Persists one chat message for the current user. The Python agent uses this endpoint for both user and assistant messages.

Request body:

```json
{
  "role": "assistant",
  "content": "Đã ghi khoản chi 20.000₫ cho Ăn uống.",
  "intent": "expense",
  "transactionId": "4a3382bc-f19a-4c29-8b90-2e63b7d7e3f4"
}
```

Fields:

- `role`: required, `user` or `assistant`.
- `content`: required, trimmed, max 4000 characters.
- `intent`: optional, one of `income`, `expense`, or `unknown`.
- `transactionId`: optional GUID. If supplied, it must reference an active (not soft-deleted) transaction owned by the current user.

Success: `201 Created` with the saved chat message and `Location: /api/chat/messages/{id}`.

```json
{
  "id": "c6dd8a2f-02d2-4f26-b8d4-8621c58bd2a8",
  "role": "assistant",
  "content": "Đã ghi khoản chi 20.000₫ cho Ăn uống.",
  "intent": "expense",
  "transactionId": "4a3382bc-f19a-4c29-8b90-2e63b7d7e3f4",
  "createdAt": "2026-06-27T02:31:00Z"
}
```

Error cases:

- `401 Unauthorized` for missing, invalid, or expired bearer token.
- `400 Bad Request` for invalid `role`, invalid `intent`, empty/too-long `content`, or malformed `transactionId`. Domain/application validation uses Problem Details; malformed JSON/GUID binding uses ASP.NET's framework response.
- `404 Not Found` Problem Details when `transactionId` does not exist, is deleted, or is not owned by the current user.
