# 0015 Enable Model-Backed Media Inference

## Status

Accepted

## Context

The requirements call for audio transcription and image understanding as part of the multimodal incident pipeline. The .NET backend already stores media and calls the Python AI service through stable `/v1/audio/transcriptions` and `/v1/images/analyze` contracts.

## Decision

Keep the .NET backend contract unchanged and add lazy-loaded Hugging Face pipelines inside the Python AI service when `USE_HF_MODELS=true`.

Audio transcription uses the `automatic-speech-recognition` pipeline configured by `ASR_MODEL`. Image analysis uses the configured `VISION_TASK` and `VISION_MODEL`, with zero-shot incident labels mapped back to CivicSignal categories. Both endpoints retain deterministic fallback behavior when models are disabled or unavailable.

The AI service container installs `ffmpeg` for browser audio decoding and `Pillow` for image loading.

## Consequences

The backend, frontend, worker, RabbitMQ, S3/MinIO, and PostgreSQL layers do not need changes when real models are enabled. First model-backed inference may be slower because weights are downloaded and loaded lazily. Local development remains lightweight with `AI_USE_HF_MODELS=false`.
