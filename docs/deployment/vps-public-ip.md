# VPS HTTP Deployment with Docker Compose

This guide documents a temporary first-test deployment on a VPS with public IP `23.100.97.60` using the current `docker-compose.yml`.

> This is an HTTP/IP deployment for validation only. For ongoing use, put the app behind a domain, HTTPS, and a reverse proxy such as Nginx, Caddy, or Traefik.

## Public URLs

- Browser app: `http://23.100.97.60:3000`
- Backend API from browser: `http://23.100.97.60:5000`
- Python agent from browser: `http://23.100.97.60:8000`

PostgreSQL and pgAdmin are intentionally bound to localhost by Compose:

- PostgreSQL: `127.0.0.1:${POSTGRES_PORT:-5432}:5432`
- pgAdmin: `127.0.0.1:${PGADMIN_PORT:-8080}:80`

Do not expose PostgreSQL or pgAdmin publicly on the VPS.

## Required environment

Set these values in the VPS shell or a local `.env` file next to `docker-compose.yml` before starting the stack:

```env
NEXT_PUBLIC_API_URL=http://23.100.97.60:5000
NEXT_PUBLIC_API_BASE_URL=http://23.100.97.60:5000
NEXT_PUBLIC_AGENT_URL=http://23.100.97.60:8000

Cors__AllowedOrigins=http://23.100.97.60:3000,http://localhost:3000,http://127.0.0.1:3000
AGENT_CORS_ORIGINS=http://23.100.97.60:3000,http://localhost:3000,http://127.0.0.1:3000

BACKEND_API_URL=http://backend:8080
```

Notes:

- `NEXT_PUBLIC_*` values are browser-visible, so they must use the public VPS IP.
- Backend CORS must include the frontend origin `http://23.100.97.60:3000`.
- Agent CORS must include the frontend origin `http://23.100.97.60:3000`.
- The agent's internal backend URL remains `BACKEND_API_URL=http://backend:8080` because the agent container talks to the backend container over the Compose network.

## Firewall / security group

For the first public test, allow inbound TCP:

- `3000` for the Next.js frontend
- `5000` for the .NET backend API
- `8000` for the Python agent API
- SSH as needed for administration, usually `22`

Keep PostgreSQL `5432` and pgAdmin `8080` closed to the public internet.

## Start and inspect

From the repository root on the VPS:

```bash
docker compose config
docker compose up -d --build
docker compose ps
```

The current Compose frontend command installs dependencies, runs the production Next.js build, then starts Next with `npm run start -- --hostname 0.0.0.0`. It does **not** run `npm run dev`, so development-server HMR WebSocket errors should not appear with this Compose startup path.

After changing frontend source, dependencies, or Compose frontend settings, rebuild the frontend container:

```bash
docker compose up -d --build frontend
# or rebuild the full stack:
docker compose up -d --build
```

On very low-RAM VPS instances, the in-container `npm run build` step may need swap space or an image built on a larger machine and deployed to the VPS.

## Basic curl checks

Run these from your workstation or the VPS after the containers are healthy:

```bash
curl -i http://23.100.97.60:3000
curl -i http://23.100.97.60:5000/swagger
curl -i http://23.100.97.60:8000/docs
```

If an endpoint differs in the running service, inspect available routes/logs with:

```bash
docker compose ps
docker compose logs backend
docker compose logs agent
docker compose logs frontend
```
