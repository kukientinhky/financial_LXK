# VPS HTTPS Deployment with Caddy and Docker Compose

This guide documents the current public deployment on a VPS with public IP `23.100.97.60`, domain `expensecraft.app`, Docker Compose, and Caddy as the public HTTPS reverse proxy.

Browsers must use HTTPS-only public URLs to avoid Mixed Content blocks. Do **not** configure frontend public URLs such as `http://23.100.97.60:5000`.

## DNS and public entrypoint

- Browser URL: `https://expensecraft.app`
- DNS: create an A record `expensecraft.app -> 23.100.97.60`.
- Caddy needs inbound ports `80` and `443` free on the host for ACME certificate issuance and renewal.
- SSH may remain open for administration.
- Compose-published app ports `3000`, `5000`, and `8000` are localhost-bound for debugging and should not be public.

## Public URLs

- Browser app: `https://expensecraft.app`
- Backend API from browser: `https://expensecraft.app/api`
- Python agent from browser: `https://expensecraft.app/agent`

Caddy routes:

| Public path | Upstream | Notes |
| --- | --- | --- |
| `/` | `frontend:3000` | Next.js app. |
| `/api/*` | `backend:8080` | .NET API behind the same HTTPS origin. |
| `/agent/*` | `agent:8000` | Strip the `/agent` prefix before proxying to the Python service. |

PostgreSQL and pgAdmin are intentionally bound to localhost by Compose:

- PostgreSQL: `127.0.0.1:${POSTGRES_PORT:-5432}:5432`
- pgAdmin: `127.0.0.1:${PGADMIN_PORT:-8080}:80`

Do not expose PostgreSQL or pgAdmin publicly on the VPS.

## Required environment

Set these values in the VPS shell or a local `.env` file next to `docker-compose.yml` before starting the stack:

```env
NEXT_PUBLIC_API_URL=https://expensecraft.app
NEXT_PUBLIC_API_BASE_URL=https://expensecraft.app
NEXT_PUBLIC_AGENT_URL=https://expensecraft.app/agent

Cors__AllowedOrigins=https://expensecraft.app,http://localhost:3000,http://127.0.0.1:3000
AGENT_CORS_ORIGINS=https://expensecraft.app,http://localhost:3000,http://127.0.0.1:3000

BACKEND_API_URL=http://backend:8080
```

Notes:

- `NEXT_PUBLIC_*` values are browser-visible and are baked into the Next.js build. Rebuild the frontend after changing them.
- To fix/prevent Mixed Content, all browser-visible API and agent URLs must use `https://expensecraft.app/...`; do not use `http://23.100.97.60:5000` or other HTTP public URLs in frontend env.
- Backend CORS must include the frontend origin `https://expensecraft.app`.
- Agent CORS must include the frontend origin `https://expensecraft.app`.
- The agent's internal backend URL remains `BACKEND_API_URL=http://backend:8080` because the agent container talks to the backend container over the Compose network.

## Firewall / security group

For public access, allow inbound TCP only for:

- `80` for Caddy HTTP challenge/redirect
- `443` for Caddy HTTPS
- SSH as needed for administration, usually `22`

Keep app/debug ports `3000`, `5000`, and `8000` closed to the public internet. They are localhost-bound in Compose for debugging, not public entrypoints. Keep PostgreSQL `5432` and pgAdmin `8080` closed to the public internet.

## Start and inspect

From the repository root on the VPS:

```bash
docker compose config
docker compose up -d --build
docker compose ps
```

The current Compose frontend command installs dependencies, runs the production Next.js build, then starts Next with `npm run start -- --hostname 0.0.0.0`. It does **not** run `npm run dev`, so development-server HMR WebSocket errors should not appear with this Compose startup path.

After changing frontend source, dependencies, Compose frontend settings, or any `NEXT_PUBLIC_*` value, rebuild the frontend container because Next.js bakes public env values at build time:

```bash
docker compose up -d --build frontend
# or rebuild the full stack:
docker compose up -d --build
```

On very low-RAM VPS instances, the in-container `npm run build` step may need swap space or an image built on a larger machine and deployed to the VPS.

## Basic curl checks

Run these from your workstation or the VPS after the containers are healthy:

```bash
curl -i https://expensecraft.app
curl -i https://expensecraft.app/api/users/me
curl -i https://expensecraft.app/agent/docs
```

If an endpoint differs in the running service, inspect available routes/logs with:

```bash
docker compose ps
docker compose logs backend
docker compose logs agent
docker compose logs frontend
```
