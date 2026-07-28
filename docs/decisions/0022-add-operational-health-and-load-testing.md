# 0022 Add Operational Health and Load Testing

## Status

Accepted

## Context

The requirements call for observability, load testing, deployment readiness, and measurable reliability. The project is not deployed yet, and full OpenTelemetry, Prometheus, Grafana, and cloud-managed monitoring would introduce infrastructure choices before the local demo is stable.

## Decision

Add a structured backend health endpoint at `/api/system/health`, return `X-Correlation-ID` on API responses, expose health status in the staff Settings UI, and add starter k6 load-test scripts for public reporting and staff dashboard read paths.

The health endpoint reports only operational status and configuration shape. It does not expose secrets, connection strings, tokens, or storage credentials.

## Consequences

The project now has a concrete readiness surface for demos and deployment smoke checks. Operators can inspect service health from the frontend, and contributors can run repeatable k6 checks locally. Full distributed tracing, metrics dashboards, model monitoring, and published load-test results remain future deployment hardening work.
