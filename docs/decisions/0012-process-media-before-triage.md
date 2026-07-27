# 0012 Process Media Before Triage

## Status

Accepted

## Context

The requirements call for multimodal incident triage where uploaded images and audio can influence final category, severity, agency routing, and reviewer evidence. Earlier milestones stored media and exposed AI service contracts, but the Worker did not yet analyze stored media before final incident triage.

## Decision

Store media analysis state and results directly on `IncidentMedia`: status, summary, transcript, detected labels, confidence, model metadata, processing time, error text, and analysis timestamp.

Add `IIncidentMediaAnalyzer` in Application. Infrastructure implements it with a Python AI-service adapter for `/v1/images/analyze` and `/v1/audio/transcriptions`, plus a heuristic fallback for local development.

The Worker now performs `MediaAnalysis` before `TriageDraft`. It opens files through `IFileStorageService`, analyzes supported image/audio media, records failures per media item, and continues final triage even when one file cannot be analyzed.

## Consequences

The frontend still talks only to the .NET API. PostgreSQL, object storage, RabbitMQ, and AI model services remain behind backend abstractions. Triage predictions can now include text, image labels, and audio transcript evidence without changing controllers when real Hugging Face models are added.
