# 0020 Add Model Lab Classifier

## Status

Accepted

## Context

The requirements emphasize AI exposure, explainability, and portfolio value. The backend already has triage, duplicate detection, media analysis boundaries, and AI evaluation baselines, but users also need a simple way to inspect how a text classifier reaches a decision.

## Decision

Add a public Application-layer `IModelLabService` and expose it through `POST /api/model-lab/analyze`.

The first implementation is deterministic and local. It tokenizes complaint text, normalizes terms, assigns stable token IDs, builds a hashing-trick embedding preview, scores category profiles with weighted terms, applies softmax, and returns the winning category, agency, severity, confidence, and evidence terms.

Add shared frontend workbench pages at `/public/model-lab` and `/admin/model-lab` so citizens, staff, and reviewers can see tokenization, embedding features, logits, probabilities, and routing decisions without direct model or database access.

## Consequences

The feature demonstrates AI internals without requiring model downloads, external API keys, or a Python service. Future Hugging Face or OpenAI classifiers can replace the deterministic baseline behind the same API contract while preserving the frontend experience and Clean Architecture boundaries.
