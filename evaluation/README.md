# CivicSignal AI Evaluation

This folder contains repeatable baseline evaluation fixtures and scripts for the AI-facing parts of CivicSignal.

## Scope

Current coverage:

- Text classification: accuracy, macro-F1, per-category precision/recall, and confusion matrix.
- Duplicate detection: precision, recall, F1, Recall@5, and false-merge rate.
- Forecasting: MAE, RMSE, and MAPE against a moving-average/trend baseline.
- Audio transcription word error rate and latency.
- Image analysis precision, recall, human-agreement rate, and unsupported-detection rate.
- Generated report factual consistency and reviewer acceptance.

Future coverage:

- System performance metrics such as P95 latency, queue wait time, failure rate, and cache-hit rate.

## Commands

Run the evaluator and write the Markdown report:

```bash
python3 evaluation/scripts/evaluate_baselines.py --write-report
```

Evaluate the running AI service contract and write the model-run report:

```bash
python3 evaluation/scripts/evaluate_ai_service.py --base-url http://localhost:8010 --write-report
```

The generated AI service report is written to `evaluation/reports/ai-service-results.md`. In deterministic mode this proves the API/model boundary. With Hugging Face enabled, the same command records model names, versions, latency, text triage metrics, embedding duplicate metrics, forecasting metrics, and media endpoint coverage.

To run a Hugging Face-backed comparison:

```bash
AI_SERVICE_INSTALL_HF_DEPS=true docker compose build ai-service
AI_USE_HF_MODELS=true docker compose up -d --no-build ai-service worker api
python3 evaluation/scripts/evaluate_ai_service.py \
  --base-url http://localhost:8010 \
  --embedding-dimensions 1024 \
  --skip-media \
  --write-report
```

`--skip-media` avoids downloading ASR/vision weights when the media fixtures are placeholders. Remove it after adding reviewed audio/image files.

Run the evaluator tests:

```bash
python3 -m unittest discover -s evaluation/tests
```

The generated report is written to `evaluation/reports/baseline-results.md`.

## Dataset Layout

- `datasets/classification_cases.jsonl`: labeled incident text, category, severity, and agency routing fixtures.
- `datasets/duplicate_cases.json`: query incidents with candidate duplicates and expected duplicate labels.
- `datasets/incident_volume.json`: historical counts plus holdout counts for forecasting checks.
- `datasets/audio_cases.json`: reference transcripts, predicted transcripts, language labels, and latency.
- `datasets/image_cases.json`: expected labels, predicted labels, human agreement, and unsupported predictions.
- `datasets/generated_report_cases.json`: required fields, expected facts, unsupported claims, and reviewer acceptance.

Keep these fixtures small, reviewed, and stable. Add larger imported datasets later under a separate folder so baseline regression tests remain fast.
