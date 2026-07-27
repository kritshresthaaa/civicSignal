# 0007 Store Evidence-Backed Predictions

## Status

Accepted

## Context

The requirements call for AI predictions that can be inspected by city staff. A category, agency, or duplicate score is not enough; reviewers need to see the supporting facts and model metadata behind the recommendation.

## Decision

Store evidence as child records of `TriagePrediction`.

Each prediction now records:

- model name
- model version
- prompt version
- processing time in milliseconds
- one or more `PredictionEvidence` records

Evidence records include kind, title, detail, optional confidence, and creation time. Heuristic analysis emits text, severity, routing, and media evidence. Duplicate detection adds duplicate-candidate evidence when similar incidents are found.

## Consequences

The frontend can render explainable predictions through the API without direct access to PostgreSQL, storage, queues, or model services. Future Hugging Face, OpenAI, or Python FastAPI adapters can return richer evidence through the same Application-layer `IncidentAnalysisResult` contract.
