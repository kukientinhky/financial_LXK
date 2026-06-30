# Configuration and Verification

## Docker Compose services

`docker-compose.yml` includes:

- `postgres` on localhost-only host port `5432`.
- `pgadmin` on localhost-only host port `8080`.
- `.NET backend` on localhost-bound host `http://localhost:5000` -> container port `8080`.
- `agent` on localhost-bound host `http://localhost:8000` -> container port `8000`.
- `frontend` on localhost-bound host `http://localhost:3000`; Compose runs the production Next.js path (`npm run build` then `npm run start -- --hostname 0.0.0.0`), not `npm run dev`.

In the HTTPS VPS deployment, the existing host Nginx is the public entrypoint at `https://expensecraft.app` and proxies to localhost-bound Compose services. The final HTTPS config is `deploy/nginx/expensecraft.app.conf`:

- `/` -> `127.0.0.1:3000`
- `/api/` -> `127.0.0.1:5000/api/`
- `/agent/` -> `127.0.0.1:8000/` with the `/agent` prefix stripped
- exact `/api` and `/agent` -> redirects to `/api/` and `/agent/`

For first-time Let's Encrypt issuance, use the HTTP-only bootstrap config at `deploy/nginx/expensecraft.app.bootstrap.conf` first, then replace it with `deploy/nginx/expensecraft.app.conf` after certificate files exist. There is no Caddy service in `docker-compose.yml`; host Nginx owns public ports `80` and `443`.

Run locally:

```bash
docker compose up
```

Because the Compose frontend starts the production Next server, development-server HMR WebSocket errors should not appear when using the current Compose command. If frontend code, dependencies, or Compose frontend settings change, rebuild with `docker compose up -d --build frontend` or rebuild the full stack with `docker compose up -d --build`.

On very low-RAM VPS instances, the frontend `npm run build` step may need swap space or an image built elsewhere and then deployed.

Validate compose wiring:

```bash
docker compose config
```

## Backend environment

The .NET backend reads standard ASP.NET configuration keys. In Docker Compose these are set with double-underscore env vars:

| Variable | Purpose | Compose value |
| --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | Enables development pipeline/Swagger. | `Development` |
| `ConnectionStrings__Database` | PostgreSQL connection for EF Core. | `Host=postgres;Port=5432;Database=mydatabase;Username=admin;Password=123456` |
| `Jwt__Issuer` | JWT issuer validation. | `ExpenseCraft` |
| `Jwt__Audience` | JWT audience validation. | `ExpenseCraft` |
| `Jwt__Secret` | HMAC signing key; change for non-dev environments. | `ExpenseCraftDevelopmentSecretKey123456789` |
| `Jwt__ExpirationInMinutes` | Access-token lifetime. | `60` |
| `Cors__AllowedOrigins` | Comma-separated browser origins allowed to call the backend. | Local: `http://localhost:3000,http://127.0.0.1:3000`; HTTPS VPS: include `https://expensecraft.app` |

Local `appsettings.json` defaults point to PostgreSQL on `localhost:5432`.

## Database migrations

When the backend runs with `ASPNETCORE_ENVIRONMENT=Development`, startup applies pending EF Core migrations automatically with `Database.MigrateAsync()`. Docker Compose sets this environment, so `docker compose up` is enough for a fresh development database.

For local/non-Docker workflows where you want to apply migrations manually, run from the repository root:

```bash
cd backend
dotnet ef database update \
  --project ExpenseCraft.Infrastructure \
  --startup-project ExpenseCraft.Api
```

## Agent environment

| Variable | Purpose | Default/source value |
| --- | --- | --- |
| `BACKEND_API_URL` | Base URL the agent uses for .NET chat and tool APIs. | Default `http://localhost:5000`; Compose `http://backend:8080` |
| `OPENROUTER_API_KEY` | Enables primary OpenRouter LLM reasoning for chat intent extraction. | Unset by default; never store keys in repo |
| `OPENROUTER_MODEL` | OpenRouter model slug for chat reasoning. | Defaults to `openai/gpt-4o-mini` |
| `AGENT_CORS_ORIGINS` | Comma-separated browser origins allowed to call the agent. | Local: `http://localhost:3000,http://127.0.0.1:3000`; HTTPS VPS: include `https://expensecraft.app` |
| `AGENT_REQUEST_TIMEOUT_SECONDS` | Backend request timeout. | `10` |

Do not commit API keys. If an OpenRouter key was pasted into a prompt, terminal output, source file, docs, or git history, rotate/revoke that key before using the service again.

## Frontend environment

| Variable | Purpose | Current behavior |
| --- | --- | --- |
| `NEXT_PUBLIC_API_URL` | Browser-visible .NET backend URL for auth, dashboard, transactions, analytics, and chat history. | Defaults to `http://localhost:5000`; Compose sets `http://localhost:5000` |
| `NEXT_PUBLIC_AGENT_URL` | Browser-visible Python agent URL for `POST /api/chat`. | Defaults to `http://localhost:8000`; Compose sets `http://localhost:8000` |
| `NEXT_PUBLIC_API_BASE_URL` | Supported fallback backend URL for login/dashboard troubleshooting. | Prefer `NEXT_PUBLIC_API_URL`. |

For Docker Compose browser access, use:

```env
NEXT_PUBLIC_API_URL=http://localhost:5000
NEXT_PUBLIC_AGENT_URL=http://localhost:8000
```

For VPS browser access, these frontend URLs must use the public HTTPS host instead of container names, localhost, or HTTP public-IP URLs. See [VPS HTTPS deployment with Host Nginx and Docker Compose](deployment/vps-public-ip.md).

For the HTTPS Nginx deployment, use only HTTPS same-origin public URLs:

```env
NEXT_PUBLIC_API_URL=https://expensecraft.app
NEXT_PUBLIC_API_BASE_URL=https://expensecraft.app
NEXT_PUBLIC_AGENT_URL=https://expensecraft.app/agent

Cors__AllowedOrigins=https://expensecraft.app,http://localhost:3000,http://127.0.0.1:3000
AGENT_CORS_ORIGINS=https://expensecraft.app,http://localhost:3000,http://127.0.0.1:3000
```

Do not set frontend env to `http://23.100.97.60:5000`; that causes browser Mixed Content failures when the app is loaded from `https://expensecraft.app`. Rebuild the frontend after changing any `NEXT_PUBLIC_*` variable because these values are baked into the Next.js build.

Login troubleshooting: the frontend accepts both `NEXT_PUBLIC_API_URL` and `NEXT_PUBLIC_API_BASE_URL` for the backend base URL. A successful login stores the JWT in `localStorage` under `expensecraft_access_token`; protected calls send it as `Authorization: Bearer <token>`.

## Verification commands

Backend:

```bash
cd backend
dotnet build
# or explicitly: dotnet build ExpenseCraft.sln
```

Agent:

```bash
cd agent
python3 -m pip install -r requirements.txt
python3 -m pytest
```

Frontend:

```bash
cd frontend
npm install
npm run lint
npm run build
```

Docker:

```bash
docker compose config
docker compose up -d --build
docker compose ps
```

Host Nginx:

First-time TLS bootstrap when `/etc/letsencrypt/live/expensecraft.app/fullchain.pem` and `privkey.pem` do not exist yet:

```bash
sudo cp deploy/nginx/expensecraft.app.bootstrap.conf /etc/nginx/sites-available/expensecraft.app.conf
sudo ln -sf /etc/nginx/sites-available/expensecraft.app.conf /etc/nginx/sites-enabled/expensecraft.app.conf
sudo nginx -t
sudo systemctl reload nginx

sudo certbot certonly --webroot -w /var/www/html -d expensecraft.app -d www.expensecraft.app

sudo cp deploy/nginx/expensecraft.app.conf /etc/nginx/sites-available/expensecraft.app.conf
sudo nginx -t
sudo systemctl reload nginx
```

If certificates already exist, skip bootstrap and use the final HTTPS config directly:

```bash
sudo cp deploy/nginx/expensecraft.app.conf /etc/nginx/sites-available/expensecraft.app.conf
sudo ln -sf /etc/nginx/sites-available/expensecraft.app.conf /etc/nginx/sites-enabled/expensecraft.app.conf
sudo nginx -t
sudo systemctl reload nginx
```

For HTTPS VPS/Nginx wiring, DNS, firewall, and Mixed Content notes, see [VPS HTTPS deployment with Host Nginx and Docker Compose](deployment/vps-public-ip.md).
