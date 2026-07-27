# 0014 Track Data Import Jobs and Worker Execution

## Status

Accepted

## Context

NYC 311 import can be slow and should not remain only a synchronous Swagger action. Operators need a visible job history with status, counts, timestamps, and failures. The frontend should still talk only to the CivicSignal API.

## Decision

Add a `DataImportJob` aggregate and job workflow:

- API creates protected NYC 311 import jobs through `POST /api/data-import-jobs/nyc311`.
- Jobs are stored in `data_import_jobs` with source, type, JSON parameters, status, counts, timestamps, and error details.
- `IDataImportJobQueue` abstracts queueing.
- RabbitMQ publishes import jobs to a separate data-import queue with retry and dead-letter routes.
- The Worker consumes RabbitMQ jobs when enabled and polls pending jobs from PostgreSQL when RabbitMQ is disabled.
- The admin frontend gets a Data Sources page for sign-in, queueing imports, and watching job history.

## Consequences

Large public-data imports no longer need to run inside the request lifecycle. Import history is auditable, demo-friendly, and isolated from live incident processing traffic.
