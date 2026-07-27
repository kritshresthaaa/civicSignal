# Repository Guidelines

## Project Structure & Module Organization

This repository is a backend-first scaffold for CivicSignal AI. Source code lives in `src/`:

- `src/CivicSignal.Api`: ASP.NET Core Web API and HTTP contracts.
- `src/CivicSignal.Application`: application services, DTOs, validators, identity constants, and application interfaces.
- `src/CivicSignal.Domain`: domain entities, value objects, and business rules.
- `src/CivicSignal.Infrastructure`: persistence, storage, messaging, AI, and external service adapters.
- `src/CivicSignal.Worker`: background processing host.

Tests live in `tests/`, split by layer. Architecture notes and decisions live in `docs/`. Infrastructure placeholders live in `infra/`. `frontend/` contains the active Next.js citizen PWA and operations dashboard. `ai-service/` contains the Python FastAPI Hugging Face-ready inference boundary.

## Build, Test, and Development Commands

Use the pinned .NET SDK from `global.json`.

```bash
/Users/kritshrestha/.dotnet/dotnet sln CivicSignal.slnx list
/Users/kritshrestha/.dotnet/dotnet restore CivicSignal.slnx
/Users/kritshrestha/.dotnet/dotnet build CivicSignal.slnx
/Users/kritshrestha/.dotnet/dotnet test CivicSignal.slnx
/Users/kritshrestha/.dotnet/dotnet run --project src/CivicSignal.Api/CivicSignal.Api.csproj
cd frontend && npm run dev
cd frontend && npm run lint
cd frontend && npm run typecheck
docker compose up --build
```

`restore` downloads NuGet packages, `build` compiles all projects, `test` runs xUnit tests, `run` starts the API, frontend commands run the Next.js app and checks, and `docker compose up --build` starts the local PostgreSQL/API/Worker/AI/frontend demo stack.

## Coding Style & Naming Conventions

Use C# nullable reference types and implicit usings as configured in the project files. Use four-space indentation. Name projects and namespaces with the `CivicSignal.*` prefix. Keep Clean Architecture dependencies pointed inward: `Domain` has no project references, `Application` depends on `Domain`, and outer projects compose infrastructure.

Use service-oriented names like `IIncidentService`, `IncidentService`, `CreateIncidentInput`, and `IncidentSearchInput`. Use `IGenericRepository<TEntity>` for shared CRUD abstractions and specialized repositories for domain-specific queries.

## Testing Guidelines

The repository uses xUnit. Put unit tests beside the layer they verify:

- Domain rules: `tests/CivicSignal.Domain.Tests`
- Application services: `tests/CivicSignal.Application.Tests`
- API behavior: `tests/CivicSignal.Api.IntegrationTests`
- Database/infrastructure behavior: `tests/CivicSignal.Infrastructure.IntegrationTests`
- Dependency rules: `tests/CivicSignal.Architecture.Tests`

Prefer test names that describe behavior, for example `Create_sets_initial_submitted_state`.

## Commit & Pull Request Guidelines

This folder is not currently initialized as a Git repository, so no historical commit convention exists. Use concise imperative commits going forward, such as `Add incident service` or `Document backend architecture`.

Pull requests should include a short summary, tests run, linked issue if available, and screenshots only for UI changes.

## Security & Configuration Tips

Do not commit real secrets. Copy `.env.example` to `.env` locally. Keep database, Redis, RabbitMQ, S3/MinIO, and AI tokens in environment variables or user secrets.

ASP.NET Core Identity types live in `src/CivicSignal.Infrastructure/Identity`. Do not move Identity EF types into `Domain`.
