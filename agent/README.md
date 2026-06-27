# ExpenseCraft Agent

Python chatbot service for Vietnamese finance messages.

## Local commands

```bash
python3 -m pip install -r requirements.txt
python3 -m uvicorn app.main:app --reload --host 0.0.0.0 --port 8000
python3 -m pytest
```

Configuration:
- `BACKEND_API_URL` defaults to `http://localhost:5000`.
- In Docker Compose it is set to `http://backend:8080`.
- `OPENROUTER_API_KEY` enables primary LLM reasoning through OpenRouter Chat Completions. Leave unset for local/test fallback parsing.
- `OPENROUTER_MODEL` selects the OpenRouter model and defaults to `openai/gpt-4o-mini`. Set it explicitly if using a DeepSeek model slug.
- Never store API keys in this repo. If a key was pasted into chat, logs, docs, source, or git history, rotate/revoke it before using the service again.

Runtime behavior:
- With `OPENROUTER_API_KEY`, chat understanding is LLM-first and requires structured JSON output.
- The agent validates intent, amount, currency, category, note, and date before any write.
- The internal MCP-like flow exposes exactly two write tools: `income` and `expense`.
- Tools call backend endpoints `/api/agent/tools/income` and `/api/agent/tools/expense`.
- Chat intents are `income`, `expense`, or `unknown`.
