# 0018 Add Weather Context and Controlled Agent Workflow

## Status

Accepted

## Context

The requirements include weather context and a controlled triage agent that uses explicit tools instead of inventing agencies, policies, case IDs, locations, weather data, or historical records.

## Decision

Add weather behind `IWeatherService` in `Application`. `Infrastructure` provides a National Weather Service adapter that is disabled by default and can be enabled with configuration.

Add `IControlledTriageAgentService` in `Application`. The workflow runs a fixed sequence of allowed tools:

- `understand_complaint`
- `collect_available_evidence`
- `get_weather`
- `search_nearby_cases`
- `retrieve_service_policy`
- `predict_responsible_agency`
- `calculate_sla_risk`
- `create_draft_work_order`

The workflow returns structured JSON, marks missing data as unavailable, persists concise `AgentTool` evidence on the latest prediction, and can run from `POST /api/incidents/{incidentId}/agent-workflow` or the background Worker.

## Consequences

The agent remains deterministic, auditable, and bounded by known backend data. Weather failures do not break incident processing; they trigger review instead of fabricated context.
