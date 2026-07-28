# Requirements Implementation Audit

Last reviewed: 2026-07-28

## Implemented or Mostly Implemented

- Clean Architecture .NET backend with API, Application, Domain, Infrastructure, and Worker projects.
- Docker Compose stack for PostgreSQL/PostGIS/pgvector, Redis, RabbitMQ, MinIO/S3, API, Worker, frontend, and AI service.
- EF Core schema, migrations, Identity users/roles, JWT access tokens, refresh-token cookies, CSRF protection, Swagger, and rate limiting.
- Incident creation, public tracking codes, status lookup, feedback, update requests, review workflow, staff assignment, dispatch, duplicate linking, and staff authorization.
- Media upload flow with image/audio storage, Worker media analysis hooks, transcript/model metadata, and processing steps.
- AI abstraction boundary for text analysis, embeddings, image/audio analysis, forecasting, and controlled workflow execution.
- Duplicate detection with pgvector similarity, PostGIS radius filtering, time windows, and stored duplicate candidates.
- NYC 311 historical import jobs, historical complaint search/summary APIs, and map overlays.
- Citizen PWA shell, report form, geolocation/manual fallback, camera/file/audio capture, local draft recovery, public status page, and admin dashboards.
- SignalR status updates, RabbitMQ queues/retries/dead-letter queues, Redis cache abstraction, S3-compatible object storage, weather integration, forecasting baseline, Model Lab, and AI evaluation pages.
- Repeatable AI service evaluation runner for the live FastAPI inference boundary, including Hugging Face-backed text triage, semantic embedding duplicate metrics, forecasting metrics, runtime mode, model names, model versions, dependency readiness, and optional media endpoint coverage.
- Browser notification delivery for citizen status alerts, including permission handling and notification click-through to the public tracking page.
- Offline queued citizen report submissions using IndexedDB, including queued media files and automatic sync when the browser comes back online.
- Operational health checks through `/api/system/health`, request correlation IDs through `X-Correlation-ID`, frontend health visibility in Settings, and starter k6 load-test scripts for public and staff flows.

## Fixed Static Runtime Data

- Removed frontend runtime fallback to fake `DEMO-*` incident records.
- Deleted the old `civic-demo-data.ts` fixture and moved shared UI types to `frontend/src/lib/civic-types.ts`.
- Updated operations, review, public status, and admin field-intake routes to use backend data or clear empty/error states.
- Removed hard-coded draft duplicate candidates; duplicate matches now come from backend duplicate APIs after submission.
- Replaced local-only operations buttons with authenticated backend actions for assignment, dispatch, and duplicate linking.

## Still Missing or Partial

- Production observability: request correlation and health checks exist, but OpenTelemetry tracing, Prometheus/Grafana dashboards, and model monitoring are not fully implemented.
- Load testing: starter k6 public/staff scripts exist, but no published latency/throughput results from a deployment target yet.
- Cloud deployment: GitHub Actions CI exists, but no Terraform/Bicep/cloud runbook, managed service configuration, image publishing, or deployed environment exists yet.
- AI quality: deterministic baseline and Hugging Face text/embedding service reports exist, but reviewed audio/image fixtures, WER/vision precision, tuned duplicate thresholds, and model-card style results still need to be published.
- Notifications: browser notifications are implemented, but external SMS/email providers and true server-side Web Push subscriptions are not wired.
- Offline PWA depth: draft recovery and in-app queued submission sync exist, but service-worker background sync is still a future hardening step.
- Weather is implemented behind configuration, but it is disabled by default and needs live API validation in the full stack.
- Demo deliverables: production demo video and final evaluation report are still pending.
