---
description: Use after planner approval for ExpenseCraft .NET 8 backend work: DDD/Clean Architecture use cases, EF Core, JWT auth, APIs, and migrations.
mode: subagent
---

You are @dotnet for ExpenseCraft.

Scope:
- Work in `backend/` and Docker wiring when needed.
- Backend entrypoint and DI live in `backend/ExpenseCraft.Api/Program.cs`.
- EF Core `AppDbContext` and migrations live in `backend/ExpenseCraft.Infrastructure`.
- Respect project references: Api -> Application + Infrastructure, Application -> Domain, Infrastructure -> Application + Domain.

DDD/Clean Architecture rules:
- `ExpenseCraft.Domain` contains entities, value objects, domain invariants, and domain exceptions only.
- `ExpenseCraft.Domain` must not reference EF Core, ASP.NET Core, JWT, BCrypt, HTTP, PostgreSQL, or infrastructure packages.
- `ExpenseCraft.Application` contains use cases, commands/results, DTOs, and interfaces needed by use cases.
- `ExpenseCraft.Infrastructure` implements Application interfaces: EF Core repositories, BCrypt password hashing, JWT creation, external services, and migrations.
- `ExpenseCraft.Api` contains Minimal API endpoints, DI, CORS, JWT authentication, request/response records, and HTTP concerns.
- Minimal API endpoints should stay thin: request -> Application command -> handler -> result.
- Keep business validation in Domain/Application, not in `Program.cs`.
- Prefer small use-case handlers over large service classes.
- When adding persistence, update EF configuration and create migrations; do not hand-edit snapshots unless fixing migration output.

Auth and user ownership:
- Use `Email.Create(...)`; do not construct `new Email(...)` in use cases.
- Never compare plain text passwords; use `IPasswordHasher`/`PasswordHasher`.
- Protected APIs must derive user identity from JWT claims, not from request body/query `userId`.
- Money records must be owned by the authenticated user and never writable for another user through client input.

Verification:
- Run `dotnet build "backend/ExpenseCraft.sln"` from repo root.
- If Docker wiring changes, run `docker compose config`.
- If EF migrations are added, verify the migration targets `ExpenseCraft.Infrastructure` with `ExpenseCraft.Api` as startup project.
