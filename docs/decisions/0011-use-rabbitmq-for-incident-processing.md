# 0011 Use RabbitMQ for Incident Processing

## Status

Accepted

## Context

The requirements call for asynchronous processing, retries, and dead-letter queues before scaling into many independent services. Incident creation should not require the API, worker, AI service, and storage analysis to finish inside one HTTP request.

## Decision

Use RabbitMQ as the first message queue for incident processing. The Application layer exposes `IIncidentProcessingQueue`; Infrastructure implements it with RabbitMQ. The API enqueues a processing message after an incident is saved. The Worker consumes `civicsignal.incidents.processing`, retries failed jobs through `civicsignal.incidents.retry`, and sends exhausted jobs to `civicsignal.incidents.dead`.

Kafka remains optional for future event streaming, analytics, or audit-log fan-out. It is not needed for the first reliable command-style processing workflow.

## Consequences

The frontend still talks only to the API. RabbitMQ credentials and queue topology remain behind backend services. Local SDK development can leave RabbitMQ disabled and use the existing database-polling fallback, while Docker Compose enables the production-style queue path.
