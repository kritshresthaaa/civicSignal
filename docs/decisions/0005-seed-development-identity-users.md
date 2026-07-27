# 0005 Seed Development Identity Users

## Status

Accepted

## Context

Protected operator workflows such as analysis, processing status updates, and review require Identity roles. Creating those roles manually in PostgreSQL slows local development and Swagger demos.

## Decision

Seed development users from `SeedUsers` configuration during API startup. The seeder only runs when enabled and defaults to `DevelopmentOnly=true`.

Seeded users default `ResetPassword=true`, so local demo credentials stay predictable when a developer reuses an existing database.

The default development accounts are:

- `admin@civicsignal.local` with `Administrator`, `Operator`, and `Reviewer`
- `operator@civicsignal.local` with `Operator`

The seeder is non-fatal. If PostgreSQL is unavailable or migrations are missing, the API still starts and logs a warning.

## Consequences

Local Swagger demos can authenticate and call protected endpoints. Production deployments must keep `SeedUsers:Enabled=false` unless explicitly provisioning controlled accounts.
