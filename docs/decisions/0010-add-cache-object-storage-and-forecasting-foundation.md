# 0010 Add Cache, Object Storage, and Forecasting Foundation

## Status

Accepted

## Context

The requirements call for production-style backend infrastructure without allowing the frontend to talk directly to PostgreSQL, storage, queues, or AI models. Incident media also needs an object-storage path, duplicate and detail reads benefit from caching, and the analytics dashboard needs workload forecasting.

## Decision

Add Redis through the Application-layer `IApplicationCache` abstraction and register `RedisApplicationCache` only when `Redis:Enabled=true`.

Add S3-compatible object storage behind `IFileStorageService`. Local disk remains the default for SDK development, while Docker Compose uses MinIO through the same interface.

Add `IIncidentForecastingService` in Application and expose `GET /api/forecasting/incident-volume` from the API. The first model is a cached moving-average/trend baseline over incident history. The Python AI service exposes a matching `/v1/forecasting/incident-volume` contract so a trained time-series model can replace the baseline later.

## Consequences

The frontend consumes one API surface and does not need direct credentials for PostgreSQL, Redis, MinIO/S3, or AI services. Local development still works without cloud accounts. Production can swap Redis endpoints, S3 providers, and AI model implementations through configuration while preserving Clean Architecture boundaries.
