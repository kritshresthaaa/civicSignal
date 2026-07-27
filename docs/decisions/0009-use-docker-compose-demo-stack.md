# 0009 Use Docker Compose Demo Stack

## Status

Accepted

## Context

CivicSignal needs to be easy to run for local development, demos, and portfolio review. The backend depends on PostgreSQL with PostGIS and pgvector, so manual setup creates friction.

## Decision

Use Docker Compose for the local demo stack:

- PostgreSQL with PostGIS and pgvector.
- Redis cache.
- RabbitMQ with management UI.
- MinIO/S3-compatible object storage.
- Python FastAPI AI service.
- ASP.NET Core API.
- Background Worker.
- Shared media volume for uploaded incident media.

The API owns optional startup EF migrations through `Database:MigrateOnStartup`. Demo records are seeded through `DemoData:Enabled` and are idempotent by checking for `[DEMO]` incidents.

## Consequences

Reviewers can run the backend with `docker compose up --build` and open Swagger without installing PostgreSQL locally. Production deployments should keep startup migrations and demo data disabled unless explicitly controlled by deployment automation.
