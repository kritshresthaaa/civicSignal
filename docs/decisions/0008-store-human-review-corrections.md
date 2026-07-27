# 0008 Store Human Review Corrections

## Status

Accepted

## Context

The requirements call for human-in-the-loop review where city staff can correct model output and produce feedback for future evaluation or training. A single approval flag is not enough because reviewers need to correct category, agency, severity, duplicate status, and explain whether the AI prediction was accepted.

## Decision

Extend the incident review workflow with correction fields and an audit table.

The incident stores the latest review summary:

- decision and note
- reviewer user id and timestamp
- corrected category
- corrected agency code
- corrected severity
- duplicate-of incident id
- accepted-prediction flag

Each review also creates an `IncidentReviewRecord` child row. The protected `/api/incidents/{incidentId}/reviews` endpoint returns review history for staff dashboards.

## Consequences

The frontend can build a real review queue and correction workflow through the API only. The stored corrections become training/evaluation data for future Hugging Face or Python AI services without changing the core domain model again.
