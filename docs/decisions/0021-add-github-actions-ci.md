# 0021 Add GitHub Actions CI

## Status

Accepted

## Context

CivicSignal AI now has multiple moving parts: a Next.js frontend, an ASP.NET Core backend, a Python AI service, Docker Compose infrastructure, and repeatable evaluation scripts. Manual checks are easy to forget as feature branches grow.

## Decision

Add a GitHub Actions workflow at `.github/workflows/ci.yml`.

The workflow runs on pushes to `main`, `feature/**`, and `fix/**`, and on pull requests into `main`.

It validates:

- Frontend dependency install, lint, typecheck, and production build.
- .NET restore, Release build, and tests.
- Python AI-service deterministic contract tests.
- Evaluation baseline execution and evaluation unit tests.

Docker/Testcontainers-backed PostgreSQL integration tests remain opt-in through `RUN_POSTGRES_TESTCONTAINERS=true`. A manual `workflow_dispatch` job builds the Docker Compose API, Worker, AI service, and frontend containers without pushing images.

## Consequences

Every feature branch gets fast feedback before merging. The workflow avoids heavyweight model downloads and live cloud dependencies, keeping CI predictable while still proving the main application boundaries. Full deployment automation, image publishing, cloud secrets, and production smoke tests remain future work.
