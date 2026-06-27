# AGENTS.md

## Project Workflow
- You are the coordinator.
- Never start implementing immediately.
- First delegate planning to `@planner`.
- After the plan is approved, route frontend work to `@frontend`.
- After the plan is approved, route backend .NET work to `@dotnet`.
- After the plan is approved, route backend Python/MCP work to `@python`.
- Before finishing, ask `@reviewer` to review all modified code.
- Before finishing, ask `@docs` to update documentation in `docs/`.
- Never bypass review.

## Project Structure
```text
financial_LXK/
|-- backend/
|   |-- ExpenseCraft.Domain/              # domain model and business rules only
|   |   |-- Users/                        # current user aggregate/value objects
|   |   |-- Transactions/                 # transaction aggregate and money rules
|   |   `-- Chat/                         # persisted chat message domain types
|   |
|   |-- ExpenseCraft.Application/         # use cases and application contracts
|   |   |-- Common/                       # shared application abstractions
|   |   |-- Users/                        # register/login user use cases
|   |   |-- Transactions/                 # create/list/delete/analytics use cases
|   |   `-- Chat/                         # chat message persistence use cases
|   |
|   |-- ExpenseCraft.Infrastructure/      # external implementations
|   |   |-- Persistence/                  # EF Core DbContext
|   |   |-- Users/                        # EF repositories
|   |   |-- Transactions/                 # EF transaction repositories
|   |   |-- Chat/                         # EF chat repositories
|   |   |-- Security/                     # BCrypt/JWT implementations
|   |   `-- Migrations/                   # EF Core migrations
|   |
|   `-- ExpenseCraft.Api/                 # Minimal API entrypoint and HTTP wiring
|       |-- Program.cs                    # DI, auth, CORS, endpoints
|       `-- appsettings.json              # local app configuration
|
|-- frontend/
|   `-- app/
|       |-- page.tsx                      # Vietnamese login/register page
|       `-- dashboard/page.tsx            # authenticated dashboard page
|
|-- agent/                               # Python/FastAPI MCP-like chatbot service
|   |-- app/
|   |   |-- main.py                       # Python service entrypoint
|   |   |-- config.py                     # environment/config loading
|   |   |-- domain/                       # business rules, no DB/API dependency
|   |   |-- application/                  # use cases, DTOs, interfaces
|   |   |-- infrastructure/               # database/API/MCP adapters
|   |   `-- presentation/                 # API/CLI/controllers/schemas
|   `-- tests/                            # Python unit tests
|
|-- .opencode/agent/                     # OpenCode subagent definitions
|-- docs/                                # documentation maintained by @docs
|   |-- backend/                         # backend API and feature docs
|   |-- chatbot/                         # Python/MCP chatbot flow docs
|   `-- frontend/                        # frontend behavior docs
`-- docker-compose.yml                   # local development infrastructure
```

## Project Purpose
- ExpenseCraft is a real expense-management product, not a static demo.
- The product manages authenticated users, income transactions, expense transactions, dashboards, and auditable money flows.
- The product direction includes a Vietnamese finance chatbot.
- The chatbot should understand messages like `tôi vừa chuyển 20k tiền ăn sáng` as transaction intent.
- The chatbot should call MCP/API tooling to write the correct transaction to the database for the authenticated user.

## Technology
- Backend: .NET 8, ASP.NET Core Minimal API, EF Core, Npgsql/PostgreSQL, BCrypt, JWT Bearer auth.
- Frontend: Next.js 16, React 19, TypeScript, Tailwind CSS 4.
- Infrastructure: Docker Compose, PostgreSQL 16, pgAdmin, Node 22 container, .NET SDK 8 container.
- Chatbot/MCP layer: Python/FastAPI service for Vietnamese finance intent parsing and transaction writes through authenticated backend APIs.

## Design System
- Frontend UI must follow `frontend/DESIGN.md`.
- For any frontend/UI task, `@frontend` must read `frontend/DESIGN.md` before editing files.
- Do not invent new colors, spacing, typography, shadows, or border radius if `frontend/DESIGN.md` already defines them.
- If a required token is missing, use the closest existing token and mention it in the final response.

## Verification
- Frontend changes: run `npm run lint` and `npm run build` from `frontend/`.
- Backend .NET changes: run `dotnet build` from `backend/`.
- Python/MCP changes: run Python tests from `agent/`.
- Documentation changes: check Markdown links and command accuracy.
