# 0004 Add AI Analysis And Local Media Storage

## Status

Accepted

## Context

CivicSignal needs a backend-first path for multimodal incident triage. The first implementation should support real media upload and a real AI-provider integration path without making local development depend on paid external APIs.

## Decision

Store uploaded incident media on local disk under `var/uploads/incident-media` and expose it through the API at `/media`. Persist metadata in `incident_media`.

Keep AI analysis behind `IAiIncidentAnalyzer`. Register the Python AI service adapter when `AiService:Enabled=true`; if the service is unavailable, fall back to the deterministic heuristic analyzer. Register the OpenAI Responses API analyzer only when `OpenAI:Enabled=true`, the Python AI service is disabled, and an API key is supplied through `OpenAI:ApiKey` or `OPENAI_API_KEY`. Otherwise, use the deterministic heuristic analyzer.

Use `ITextEmbeddingGenerator` for vector embeddings. When `AiService:Enabled=true` and `AiService:UseRemoteEmbeddings=true`, call the Python service embedding endpoint and fall back to local hashing if the service is unavailable.

Use structured JSON output for triage fields: category, severity, confidence, summary, and suggested agency code.

## Consequences

Local development and tests continue to run without secrets. Docker Compose can exercise a separate AI service without changing controllers, domain logic, or application services. Production can switch between Python/Hugging Face, OpenAI, or local fallback through configuration. A future cloud storage adapter can replace local disk without changing API controllers or domain logic.
