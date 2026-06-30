# Frontend Dashboard Behavior

The dashboard is authenticated. It reads the JWT from `localStorage` key `expensecraft_access_token` and sends protected backend requests with `Authorization: Bearer <token>`.

Runtime URLs:

- `NEXT_PUBLIC_API_URL` points the browser to the .NET backend and defaults to `http://localhost:5000`.
- `NEXT_PUBLIC_API_BASE_URL` is also supported as a fallback backend URL for login/dashboard troubleshooting; prefer `NEXT_PUBLIC_API_URL`.
- `NEXT_PUBLIC_AGENT_URL` points the browser to the Python agent and defaults to `http://localhost:8000`; Docker Compose also sets `http://localhost:8000`.

Successful login stores the JWT in `localStorage` as `expensecraft_access_token`. If login succeeds but protected calls fail, verify the backend URL env var and that requests include `Authorization: Bearer <token>`.

The login hero no longer shows the three statistic cards; the page focuses on the Vietnamese sign-in/register form and core product messaging.

## Data loading

On initial load the dashboard calls:

- `GET /api/users/me` to validate the session.
- `GET /api/analytics/summary?from&to` for the chart period.
- `GET /api/transactions?limit=20` for recent transactions.
- `GET /api/chat/messages?limit=50` for persisted chat history.

The analytics period starts on the first day of the month five months before the current month and ends today. The frontend sends `YYYY-MM-DD` dates; the backend interprets date filters and month grouping with `Asia/Ho_Chi_Minh` local semantics. The UI fills missing months with zero values so charts always show six months.

## Database-backed charts

Dashboard cards and charts use the .NET analytics response, not hard-coded demo data:

- total income, total expense, and net cashflow;
- monthly income/expense bars from `cashflowByMonth`;
- expense category breakdown from `expenseByCategory`;
- spending reasonableness from `reasonableness`.

After chat writes or transaction deletes, the dashboard refreshes analytics, recent transactions, and chat history.

## Persisted chat

Sending a message calls the Python agent:

```http
POST {NEXT_PUBLIC_AGENT_URL}/api/chat
Authorization: Bearer <token>
```

The agent persists the user and assistant messages through the backend. The dashboard temporarily shows pending local bubbles, then refreshes from `GET /api/chat/messages?limit=50` so history survives page reloads.

If the agent response has `history_save_failed: true` with a `warning`, the transaction was already committed but the assistant history write failed. The dashboard displays the warning and preserves the local assistant bubble after refreshing chat history. Do not resend the same prompt blindly because that can duplicate the money write.

Example prompt shown in the UI:

```text
tôi vừa chuyển 20k tiền ăn sáng
```

## Deleting wrong transaction entries

Recent income and expense rows both show a `Xóa` action.

Delete flow:

1. User confirms deletion.
2. Dashboard calls `DELETE /api/transactions/{id}` with the bearer token.
3. Backend soft-deletes the transaction only if it belongs to the current user.
4. Dashboard refreshes analytics, recent transactions, and chat history.

If the token expires or backend returns `401`, the dashboard clears local auth and redirects to login.
