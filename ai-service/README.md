# AI Service

Python FastAPI inference boundary for CivicSignal AI. The service runs deterministic local logic by default and switches to Hugging Face pipelines when `USE_HF_MODELS=true`.

## Local Run

```bash
python3 -m venv .venv
. .venv/bin/activate
pip install -r requirements.txt
uvicorn app.main:app --reload --port 8010
```

Health check:

```text
GET /health
```

Core contracts:

```text
POST /v1/incidents/analyze
POST /v1/text/embeddings
POST /v1/audio/transcriptions
POST /v1/images/analyze
POST /v1/forecasting/incident-volume
```

The .NET Worker calls the image and audio endpoints after uploaded files are stored in MinIO/S3 or local storage. The returned labels, transcripts, confidence, model name, model version, and processing time are stored on the incident media record and included in later triage requests.

## Hugging Face Path

Install the optional model stack before enabling model-backed inference locally:

```bash
pip install -r requirements.hf.txt
```

For Docker Compose, set `AI_SERVICE_INSTALL_HF_DEPS=true` and `AI_USE_HF_MODELS=true` before rebuilding the `ai-service` image. Model-backed inference is lazy-loaded on first use. The service now supports:

- Whisper or equivalent ASR for audio transcription through `/v1/audio/transcriptions`.
- SigLIP/CLIP/zero-shot image models for image understanding through `/v1/images/analyze`.
- BART/MNLI or a fine-tuned classifier for incident category routing.
- Sentence Transformers for semantic embeddings.
- A baseline moving-average/trend model for incident volume forecasting.

Use environment variables such as `USE_HF_MODELS`, `ASR_MODEL`, `VISION_MODEL`, `VISION_TASK`, `TEXT_MODEL`, `EMBEDDING_MODEL`, and `HF_DEVICE` to switch model implementations without changing the .NET backend.

Example:

```bash
USE_HF_MODELS=true \
ASR_MODEL=openai/whisper-small \
VISION_MODEL=google/siglip-base-patch16-224 \
VISION_TASK=zero-shot-image-classification \
uvicorn app.main:app --reload --port 8010
```

The first model-backed request downloads weights from Hugging Face, so startup remains fast but the first inference call can take longer.

## Evaluation

From the repository root, evaluate the running service:

```bash
python3 evaluation/scripts/evaluate_ai_service.py \
  --base-url http://localhost:8010 \
  --embedding-dimensions 1024 \
  --skip-media \
  --write-report
```

The report is written to `evaluation/reports/ai-service-results.md`. A deterministic run proves the contract; a `USE_HF_MODELS=true` run provides the Hugging Face comparison numbers for portfolio/resume use. Remove `--skip-media` once reviewed audio/image fixtures are available.

## Tests

```bash
PYTHONPATH=. python -m unittest discover -s tests
```
