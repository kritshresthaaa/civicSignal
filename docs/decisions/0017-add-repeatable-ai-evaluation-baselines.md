# 0017 Add Repeatable AI Evaluation Baselines

## Status

Accepted

## Context

The requirements call for measurable AI quality across text classification, duplicate detection, image/audio analysis, forecasting, generated reports, and system performance. The backend already exposes AI service boundaries and local heuristic fallbacks, but it needs a repeatable way to compare future Hugging Face, pgvector, and forecasting changes.

## Decision

Add an `evaluation/` module outside the runtime backend. It contains fixed fixtures, a stdlib Python evaluator, unit tests, and generated Markdown reports.

The baseline covers:

- Text category, severity, and agency routing quality.
- Duplicate detection precision, recall, F1, Recall@5, and false-merge rate.
- Incident-volume forecasting MAE, RMSE, and MAPE.
- Audio word error rate, language accuracy, and transcription latency.
- Image precision, recall, human agreement, and unsupported-detection rate.
- Generated-report required-field completion, factual consistency, unsupported-claim rate, and reviewer acceptance.

## Consequences

Evaluation can run locally without Docker, cloud credentials, or model downloads. This keeps regression checks fast and portfolio-friendly.

Future model-backed evaluation should reuse this structure, add imported NYC 311 holdout datasets, and store model version metadata with every run.
