# CivicSignal AI Portfolio Evaluation Report

Generated: `2026-07-27`

This report summarizes the current feature and AI-quality evidence for CivicSignal AI. It is intended for GitHub, LinkedIn, resume preparation, and demo planning. Use the numbers below only with the context shown here.

## Evidence Sources

- Deterministic local baseline: [`evaluation/reports/baseline-results.md`](../../evaluation/reports/baseline-results.md), regenerated `2026-07-27`.
- Last captured AI-service run: [`evaluation/reports/ai-service-results.md`](../../evaluation/reports/ai-service-results.md), generated `2026-07-25`.
- Requirements audit: [`docs/architecture/requirements-implementation-audit.md`](../architecture/requirements-implementation-audit.md).

The deterministic report proves repeatable evaluation logic over reviewed fixtures. The AI-service report proves the FastAPI/Hugging Face-ready contract and records model-backed behavior from the last available run.

## Implemented Portfolio Features

- Mobile-first citizen PWA with public reporting, feed, status tracking, media upload, geolocation, draft recovery, and offline queued submissions.
- Staff operations console with dashboard, map filtering, review queue, duplicate inspection, assignment, dispatch, analytics, data import monitoring, and model lab.
- ASP.NET Core Clean Architecture backend with EF Core, PostgreSQL, PostGIS, pgvector, Identity auth, JWT access tokens, refresh-token cookies, CSRF protection, rate limiting, Swagger, and SignalR.
- Async processing stack with RabbitMQ, Redis, MinIO/S3-compatible storage, and a .NET Worker.
- Python FastAPI AI service boundary for text triage, embeddings, image analysis, audio transcription, and forecasting.
- Evidence-backed triage predictions, controlled agent workflow, weather/geocoding integrations, NYC 311 historical context, and repeatable evaluation scripts.

## Metric Snapshot

| Area | Current result | How to interpret |
| --- | ---: | --- |
| Deterministic category classification | 96.7% macro-F1 | Local fixture baseline, not a production model claim. |
| Last AI-service text triage | 92.7% macro-F1 | Hugging Face-backed comparison from the captured run. |
| Deterministic duplicate detection | 100.0% F1, 0.0% false-merge rate | Small fixture baseline; useful as a regression guard. |
| Last AI-service embedding duplicates | 70.6% F1, 45.5% false-merge rate | Needs threshold tuning before portfolio claims about duplicate quality. |
| Deterministic forecasting | MAE 0.95, MAPE 3.5% | Transparent moving-average/trend baseline. |
| Last AI-service forecasting | MAE 2.91, MAPE 10.4% | Contract works, but it does not beat the local baseline yet. |
| Deterministic audio fixtures | 13.3% WER | Fixture-level ASR benchmark. |
| Last AI-service audio | 11.1% WER | Whisper-backed captured run; needs larger reviewed audio set. |
| Deterministic image fixtures | 100.0% F1 | Fixture-level baseline, not proof of real-world vision accuracy. |
| Last AI-service image | 54.5% F1 | Needs better model/labels before claiming strong image understanding. |
| Generated reports | 93.3% factual consistency, 6.2% unsupported claims | Useful quality gate; needs more reviewer-labeled cases. |

## Safe LinkedIn/GitHub Claims

- Built a full-stack multimodal city incident intelligence platform with a Next.js PWA, ASP.NET Core API, FastAPI AI service, PostgreSQL/PostGIS/pgvector, RabbitMQ, Redis, and S3-compatible storage.
- Implemented AI-assisted triage with evidence-backed predictions, duplicate detection, geospatial search, human review, and controlled workflow orchestration.
- Added repeatable evaluation scripts covering classification, duplicate detection, forecasting, audio, image, and generated-report quality.
- Captured baseline metrics and model-backed comparison reports instead of making unmeasured AI claims.

## Claims to Avoid for Now

- Do not claim the system is production deployed.
- Do not claim it serves a real city or municipality.
- Do not claim perfect duplicate detection outside the small deterministic fixture set.
- Do not claim production-grade image understanding until the image fixtures and model choice improve.
- Do not claim cloud reliability, observability, or load-tested scale yet.

## Recommended Next Work

1. Add larger reviewed audio and image fixtures, then rerun AI-service evaluation without `--skip-media`.
2. Tune embedding duplicate thresholds to reduce false merges and document precision/recall tradeoffs.
3. Add GitHub Actions for frontend checks, .NET tests, Python tests, and baseline evaluation.
4. Add OpenTelemetry, Prometheus, and Grafana so latency, queue time, and failure rate are visible.
5. Deploy a staging environment and capture a short demo video using measured metrics.

## Reproduction Commands

```bash
python3 evaluation/scripts/evaluate_baselines.py --write-report
python3 -m unittest discover -s evaluation/tests
```

When the AI service is running:

```bash
python3 evaluation/scripts/evaluate_ai_service.py \
  --base-url http://localhost:8010 \
  --embedding-dimensions 1024 \
  --write-report
```
