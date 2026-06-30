# VPS HTTPS Deployment with Host Nginx and Docker Compose

This guide documents the current public deployment on a VPS with public IP `23.100.97.60`, domain `expensecraft.app`, Docker Compose, and the existing host Nginx as the public HTTPS reverse proxy.

Browsers must use HTTPS-only public URLs to avoid Mixed Content blocks. Do **not** configure frontend public URLs such as `http://23.100.97.60:5000`.

## DNS and public entrypoint

- Browser URL: `https://expensecraft.app`
- DNS: create an A record `expensecraft.app -> 23.100.97.60`.
- Host Nginx owns public ports `80` and `443` for HTTP redirects, HTTPS, and ACME certificate issuance/renewal.
- SSH may remain open for administration.
- Compose-published app ports `3000`, `5000`, and `8000` are localhost-bound for Nginx proxying/debugging and should not be public.
- There is no Caddy service in `docker-compose.yml` for this deployment.

## Public URLs

- Browser app: `https://expensecraft.app`
- Backend API from browser: `https://expensecraft.app/api`
- Python agent from browser: `https://expensecraft.app/agent`

Host Nginx routes, using the final HTTPS config at `deploy/nginx/expensecraft.app.conf`:

| Public path | Upstream | Notes |
| --- | --- | --- |
| `/` | `127.0.0.1:3000` | Next.js app. |
| `/api/` | `127.0.0.1:5000/api/` | .NET API behind the same HTTPS origin. |
| `/agent/` | `127.0.0.1:8000/` | Strip the `/agent` prefix before proxying to the Python service. |

The exact paths `/api` and `/agent` redirect to `/api/` and `/agent/` so browser clients land on the proxied route with the expected trailing slash.

PostgreSQL and pgAdmin are intentionally bound to localhost by Compose:

- PostgreSQL: `127.0.0.1:${POSTGRES_PORT:-5432}:5432`
- pgAdmin: `127.0.0.1:${PGADMIN_PORT:-8080}:80`
- Frontend: `127.0.0.1:3000:3000`
- Backend: `127.0.0.1:5000:8080`
- Agent: `127.0.0.1:8000:8000`

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

- `80` for Nginx HTTP challenge/redirect
- `443` for Nginx HTTPS
- SSH as needed for administration, usually `22`

Keep app/debug ports `3000`, `5000`, and `8000` closed to the public internet. They are localhost-bound in Compose for debugging, not public entrypoints. Keep PostgreSQL `5432` and pgAdmin `8080` closed to the public internet.

## Configure Nginx, TLS, start, and inspect

From the repository root on the VPS:

### First-time TLS bootstrap

If the Let's Encrypt certificate files do not exist yet, start with the HTTP-only bootstrap config. The final HTTPS config references explicit certificate paths, so it should be enabled only after the first certificate has been issued.

```bash
sudo cp deploy/nginx/expensecraft.app.bootstrap.conf /etc/nginx/sites-available/expensecraft.app.conf
sudo ln -sf /etc/nginx/sites-available/expensecraft.app.conf /etc/nginx/sites-enabled/expensecraft.app.conf
sudo nginx -t
sudo systemctl reload nginx

# Issue the first certificate using the webroot served by the bootstrap config.
sudo certbot certonly --webroot -w /var/www/html -d expensecraft.app -d www.expensecraft.app

# Replace the bootstrap config with the final HTTPS reverse proxy config.
sudo cp deploy/nginx/expensecraft.app.conf /etc/nginx/sites-available/expensecraft.app.conf
sudo nginx -t
sudo systemctl reload nginx

docker compose config
docker compose up -d --build
docker compose ps
```

An equivalent Certbot flow is acceptable if it creates the same certificate files used by `deploy/nginx/expensecraft.app.conf`:

- `/etc/letsencrypt/live/expensecraft.app/fullchain.pem`
- `/etc/letsencrypt/live/expensecraft.app/privkey.pem`

### Existing certificates

If those certificate files already exist, skip the bootstrap config and use the final HTTPS config directly:

```bash
sudo cp deploy/nginx/expensecraft.app.conf /etc/nginx/sites-available/expensecraft.app.conf
sudo ln -sf /etc/nginx/sites-available/expensecraft.app.conf /etc/nginx/sites-enabled/expensecraft.app.conf
sudo nginx -t
sudo systemctl reload nginx

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
