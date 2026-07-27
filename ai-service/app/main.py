from __future__ import annotations

import hashlib
import importlib.util
import io
import logging
import math
import os
import re
import subprocess
import tempfile
import time
from dataclasses import dataclass
from datetime import date, timedelta
from pathlib import Path
from typing import Any
from typing import Annotated

from fastapi import FastAPI, File, UploadFile
from pydantic import BaseModel, Field
from starlette.concurrency import run_in_threadpool

logger = logging.getLogger("civicsignal-ai-service")

app = FastAPI(
    title="CivicSignal AI Service",
    version="0.1.0",
    summary="Hugging Face-ready inference boundary for CivicSignal incident triage.",
)

STOP_WORDS = {
    "a",
    "an",
    "and",
    "are",
    "around",
    "at",
    "by",
    "for",
    "from",
    "in",
    "is",
    "near",
    "of",
    "on",
    "the",
    "there",
    "to",
    "with",
}

CATEGORY_LABELS = {
    "road damage or pothole": "RoadDamage",
    "flooding or blocked drain": "Flooding",
    "streetlight or traffic signal issue": "Streetlight",
    "trash debris or illegal dumping": "Sanitation",
    "graffiti or vandalism": "Graffiti",
    "fallen tree or branch hazard": "TreeHazard",
    "general city service incident": "GeneralIncident",
}

IMAGE_CATEGORY_LABELS = {
    "pothole or damaged road surface": "RoadDamage",
    "cracked asphalt or damaged sidewalk": "RoadDamage",
    "flooded road or blocked storm drain": "Flooding",
    "broken streetlight or traffic signal": "Streetlight",
    "trash debris or illegal dumping": "Sanitation",
    "graffiti or vandalism": "Graffiti",
    "fallen tree branch blocking public way": "TreeHazard",
    "general city service incident": "GeneralIncident",
}

_zero_shot_classifier: Any | None = None
_embedding_model: Any | None = None
_asr_transcriber: Any | None = None
_vision_classifier: Any | None = None


@dataclass(frozen=True)
class HfAudioTranscription:
    text: str
    language: str
    confidence: float
    model_name: str
    model_version: str
    detail: str


@dataclass(frozen=True)
class HfImageLabel:
    name: str
    confidence: float
    raw_label: str


@dataclass(frozen=True)
class HfImageAnalysis:
    labels: list[HfImageLabel]
    model_name: str
    model_version: str
    detail: str


class IncidentMediaDescriptor(BaseModel):
    id: str
    fileName: str
    contentType: str
    storageUri: str
    mediaType: str
    analysisStatus: str = "Pending"
    analysisSummary: str | None = None
    transcript: str | None = None
    detectedLabels: list[str] = Field(default_factory=list)


class IncidentAnalysisRequest(BaseModel):
    incidentId: str
    description: str = Field(min_length=1)
    latitude: float
    longitude: float
    media: list[IncidentMediaDescriptor] = Field(default_factory=list)


class EvidenceItem(BaseModel):
    kind: str
    title: str
    detail: str
    confidence: float | None = None


class IncidentAnalysisResponse(BaseModel):
    category: str
    severity: str
    confidence: float = Field(ge=0, le=1)
    summary: str
    suggestedAgencyCode: str
    modelName: str
    modelVersion: str | None = None
    promptVersion: str | None = None
    processingTimeMilliseconds: int
    evidence: list[EvidenceItem]


class EmbeddingRequest(BaseModel):
    text: str = Field(min_length=1)
    dimensions: int = Field(default=1024, ge=128, le=2000)


class EmbeddingResponse(BaseModel):
    embedding: list[float]
    modelName: str
    modelVersion: str


class IncidentVolumeHistoryPoint(BaseModel):
    date: str
    count: int = Field(ge=0)


class IncidentVolumeForecastRequest(BaseModel):
    history: list[IncidentVolumeHistoryPoint] = Field(default_factory=list)
    horizonDays: int = Field(default=7, ge=1, le=30)


class IncidentVolumeForecastPoint(BaseModel):
    date: str
    actualCount: int | None = None
    forecastCount: float
    lowerBound: int
    upperBound: int


class IncidentVolumeForecastResponse(BaseModel):
    modelName: str
    modelVersion: str
    processingTimeMilliseconds: int
    forecast: list[IncidentVolumeForecastPoint]
    explanation: str


@app.get("/health")
def health() -> dict[str, object]:
    dependencies = hf_dependency_status()

    return {
        "status": "ok",
        "service": "civicsignal-ai-service",
        "mode": "huggingface-ready" if use_hf_models() else "deterministic-local",
        "huggingFace": {
            "enabled": use_hf_models(),
            "dependenciesReady": all(dependencies.values()),
            "dependencies": dependencies,
        },
        "models": {
            "asr": os.getenv("ASR_MODEL", "openai/whisper-small"),
            "vision": os.getenv("VISION_MODEL", "google/siglip-base-patch16-224"),
            "visionTask": os.getenv("VISION_TASK", "zero-shot-image-classification"),
            "text": os.getenv("TEXT_MODEL", "facebook/bart-large-mnli"),
            "embedding": os.getenv("EMBEDDING_MODEL", "sentence-transformers/all-MiniLM-L6-v2"),
        },
    }


@app.post("/v1/incidents/analyze", response_model=IncidentAnalysisResponse)
def analyze_incident(request: IncidentAnalysisRequest) -> IncidentAnalysisResponse:
    started = time.perf_counter()
    analysis_text = build_analysis_text(request)
    description = analysis_text.lower()
    hf_classification = try_hf_text_classification(analysis_text) if use_hf_models() else None
    category = hf_classification["category"] if hf_classification else determine_category(description, request.media)
    severity = determine_severity(description)
    agency = determine_agency(category)
    confidence = hf_classification["confidence"] if hf_classification else determine_confidence(category, request.media)
    summary = build_summary(category, severity, agency, request.description)
    evidence = build_evidence(description, category, severity, agency, request.media)
    model_name = "civicsignal-ai-service-local-triage"
    model_version = "0.1.0"

    if hf_classification:
        model_name = hf_classification["modelName"]
        model_version = hf_classification["modelVersion"]
        evidence.insert(
            0,
            EvidenceItem(
                kind="Text",
                title="Hugging Face zero-shot classification",
                detail=hf_classification["detail"],
                confidence=confidence,
            ),
        )

    return IncidentAnalysisResponse(
        category=category,
        severity=severity,
        confidence=confidence,
        summary=summary,
        suggestedAgencyCode=agency,
        modelName=model_name,
        modelVersion=model_version,
        promptVersion="ai-service-triage-contract-v1",
        processingTimeMilliseconds=elapsed_ms(started),
        evidence=evidence,
    )


@app.post("/v1/text/embeddings", response_model=EmbeddingResponse)
def create_embedding(request: EmbeddingRequest) -> EmbeddingResponse:
    hf_embedding = try_hf_embedding(request.text, request.dimensions) if use_hf_models() else None
    if hf_embedding is not None:
        return EmbeddingResponse(
            embedding=hf_embedding,
            modelName=os.getenv("EMBEDDING_MODEL", "sentence-transformers/all-MiniLM-L6-v2"),
            modelVersion="huggingface",
        )

    return EmbeddingResponse(
        embedding=hash_embedding(request.text, request.dimensions),
        modelName="civicsignal-ai-service-hashing-embedding",
        modelVersion="0.1.0",
    )


@app.post("/v1/audio/transcriptions")
async def transcribe_audio(file: Annotated[UploadFile, File(...)]) -> dict[str, object]:
    started = time.perf_counter()
    file_name = file.filename or "audio-upload"
    content = await file.read()
    hf_result = (
        await run_in_threadpool(try_hf_audio_transcription, content, file_name)
        if use_hf_models()
        else None
    )

    if hf_result is not None:
        return {
            "text": hf_result.text,
            "language": hf_result.language,
            "confidence": hf_result.confidence,
            "modelName": hf_result.model_name,
            "modelVersion": hf_result.model_version,
            "processingTimeMilliseconds": elapsed_ms(started),
            "evidence": [
                {
                    "kind": "Audio",
                    "title": "Hugging Face ASR completed",
                    "detail": hf_result.detail,
                    "confidence": hf_result.confidence,
                }
            ],
        }

    transcript = infer_audio_transcript(file_name)
    confidence = 0.42 if transcript else 0.0
    fallback_detail = (
        "Hugging Face ASR is disabled. Filename-based local transcript returned."
        if not use_hf_models()
        else "Hugging Face ASR was unavailable. Filename-based local transcript returned."
    )

    return {
        "text": transcript,
        "language": "en" if transcript else "unknown",
        "confidence": confidence,
        "modelName": os.getenv("ASR_MODEL", "openai/whisper-small"),
        "modelVersion": "not-loaded",
        "processingTimeMilliseconds": elapsed_ms(started),
        "evidence": [
            {
                "kind": "Audio",
                "title": "Audio transcription contract available",
                "detail": fallback_detail,
                "confidence": confidence,
            }
        ],
    }


@app.post("/v1/images/analyze")
async def analyze_image(file: Annotated[UploadFile, File(...)]) -> dict[str, object]:
    started = time.perf_counter()
    file_name = file.filename or "image-upload"
    content = await file.read()
    hf_result = (
        await run_in_threadpool(try_hf_image_analysis, content, file_name)
        if use_hf_models()
        else None
    )

    if hf_result is not None:
        return {
            "labels": [
                {"name": label.name, "confidence": label.confidence}
                for label in hf_result.labels
            ],
            "modelName": hf_result.model_name,
            "modelVersion": hf_result.model_version,
            "processingTimeMilliseconds": elapsed_ms(started),
            "evidence": [
                {
                    "kind": "Image",
                    "title": "Hugging Face vision analysis completed",
                    "detail": hf_result.detail,
                    "confidence": hf_result.labels[0].confidence if hf_result.labels else 0.0,
                }
            ],
        }

    label = determine_category(file_name.lower(), [])
    fallback_detail = (
        "Hugging Face vision models are disabled. Filename-based image category returned."
        if not use_hf_models()
        else "Hugging Face vision analysis was unavailable. Filename-based image category returned."
    )

    return {
        "labels": [{"name": label, "confidence": 0.35}],
        "modelName": os.getenv("VISION_MODEL", "google/siglip-base-patch16-224"),
        "modelVersion": "not-loaded",
        "processingTimeMilliseconds": elapsed_ms(started),
        "evidence": [
            {
                "kind": "Image",
                "title": "Vision contract available",
                "detail": fallback_detail,
                "confidence": 0.35,
            }
        ],
    }


@app.post("/v1/forecasting/incident-volume", response_model=IncidentVolumeForecastResponse)
def forecast_incident_volume(request: IncidentVolumeForecastRequest) -> IncidentVolumeForecastResponse:
    started = time.perf_counter()

    return IncidentVolumeForecastResponse(
        modelName="civicsignal-ai-service-moving-average-trend",
        modelVersion="0.1.0",
        processingTimeMilliseconds=elapsed_ms(started),
        forecast=baseline_incident_volume_forecast(request.history, request.horizonDays),
        explanation="Baseline AI-service forecast uses recent volume, trend, and day-of-week weighting. Replace with a trained time-series model after evaluation data exists.",
    )


def use_hf_models() -> bool:
    return os.getenv("USE_HF_MODELS", "false").lower() in {"1", "true", "yes"}


def hf_dependency_status() -> dict[str, bool]:
    return {
        "transformers": has_module("transformers"),
        "sentenceTransformers": has_module("sentence_transformers"),
        "torch": has_module("torch"),
        "pillow": has_module("PIL"),
        "sentencePiece": has_module("sentencepiece"),
        "protobuf": has_module("google.protobuf"),
    }


def has_module(module_name: str) -> bool:
    try:
        return importlib.util.find_spec(module_name) is not None
    except ModuleNotFoundError:
        return False


def try_hf_text_classification(text: str) -> dict[str, Any] | None:
    global _zero_shot_classifier

    try:
        if _zero_shot_classifier is None:
            from transformers import pipeline

            _zero_shot_classifier = pipeline(
                "zero-shot-classification",
                model=os.getenv("TEXT_MODEL", "facebook/bart-large-mnli"),
            )

        labels = list(CATEGORY_LABELS.keys())
        result = _zero_shot_classifier(text, labels, multi_label=False)
        label = str(result["labels"][0])
        score = float(result["scores"][0])
        category = CATEGORY_LABELS.get(label, "GeneralIncident")

        return {
            "category": category,
            "confidence": round(max(0.5, min(0.98, score)), 2),
            "detail": f"Top label '{label}' mapped to {category}.",
            "modelName": os.getenv("TEXT_MODEL", "facebook/bart-large-mnli"),
            "modelVersion": "huggingface",
        }
    except Exception as exception:
        logger.warning("Hugging Face text classification unavailable: %s", exception)
        return None


def try_hf_embedding(text: str, dimensions: int) -> list[float] | None:
    global _embedding_model

    try:
        if _embedding_model is None:
            from sentence_transformers import SentenceTransformer

            _embedding_model = SentenceTransformer(
                os.getenv("EMBEDDING_MODEL", "sentence-transformers/all-MiniLM-L6-v2")
            )

        vector = _embedding_model.encode(text, normalize_embeddings=True)
        return resize_embedding([float(value) for value in vector], dimensions)
    except Exception as exception:
        logger.warning("Hugging Face embedding unavailable: %s", exception)
        return None


def try_hf_audio_transcription(content: bytes, file_name: str) -> HfAudioTranscription | None:
    global _asr_transcriber

    if not content:
        return None

    model_name = os.getenv("ASR_MODEL", "openai/whisper-small")

    try:
        if _asr_transcriber is None:
            from transformers import pipeline

            _asr_transcriber = pipeline(
                "automatic-speech-recognition",
                model=model_name,
                **hf_pipeline_kwargs(),
            )

        with tempfile.TemporaryDirectory() as temporary_directory:
            temporary_path = Path(temporary_directory)
            input_path = temporary_path / f"upload{safe_upload_suffix(file_name, '.wav')}"
            normalized_path = temporary_path / "normalized.wav"
            input_path.write_bytes(content)
            audio_path = normalize_audio_for_asr(input_path, normalized_path)
            result = _asr_transcriber(str(audio_path))

        text = extract_text(result)
        confidence = extract_confidence(result)
        language = extract_language(result, text)
        detail = (
            f"Uploaded audio transcribed with {model_name}."
            if text
            else f"Uploaded audio was analyzed by {model_name}, but no speech text was detected."
        )

        return HfAudioTranscription(
            text=text,
            language=language,
            confidence=round(confidence if confidence is not None else (0.78 if text else 0.0), 3),
            model_name=model_name,
            model_version="huggingface",
            detail=detail,
        )
    except Exception as exception:
        logger.warning("Hugging Face audio transcription unavailable: %s", exception)
        return None


def try_hf_image_analysis(content: bytes, file_name: str) -> HfImageAnalysis | None:
    global _vision_classifier

    if not content:
        return None

    model_name = os.getenv("VISION_MODEL", "google/siglip-base-patch16-224")
    task = os.getenv("VISION_TASK", "zero-shot-image-classification")
    display_name = Path(file_name).name or "image upload"

    try:
        from PIL import Image

        if _vision_classifier is None:
            from transformers import pipeline

            _vision_classifier = pipeline(
                task,
                model=model_name,
                **hf_pipeline_kwargs(),
            )

        with Image.open(io.BytesIO(content)) as image:
            rgb_image = image.convert("RGB")

        if task == "zero-shot-image-classification":
            result = _vision_classifier(
                rgb_image,
                candidate_labels=list(IMAGE_CATEGORY_LABELS.keys()),
            )
        else:
            result = _vision_classifier(rgb_image)

        labels = build_hf_image_labels(result)
        detail = (
            "Top visual signal(s): "
            + ", ".join(f"{label.raw_label} ({label.confidence * 100:.0f}%)" for label in labels[:3])
            + "."
            if labels
            else f"Uploaded image '{display_name}' was analyzed by {model_name}, but no incident labels were returned."
        )

        return HfImageAnalysis(
            labels=labels,
            model_name=model_name,
            model_version="huggingface",
            detail=detail,
        )
    except Exception as exception:
        logger.warning("Hugging Face image analysis unavailable: %s", exception)
        return None


def hf_pipeline_kwargs() -> dict[str, object]:
    kwargs: dict[str, object] = {}
    device = os.getenv("HF_DEVICE", "").strip()

    if re.fullmatch(r"-?\d+", device):
        kwargs["device"] = int(device)

    return kwargs


def safe_upload_suffix(file_name: str, default_suffix: str) -> str:
    suffix = Path(file_name).suffix.lower()
    if re.fullmatch(r"\.[a-z0-9]{1,8}", suffix):
        return suffix

    return default_suffix


def normalize_audio_for_asr(input_path: Path, output_path: Path) -> Path:
    command = [
        "ffmpeg",
        "-y",
        "-loglevel",
        "error",
        "-i",
        str(input_path),
        "-ar",
        "16000",
        "-ac",
        "1",
        "-f",
        "wav",
        str(output_path),
    ]

    try:
        subprocess.run(command, check=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        if output_path.exists() and output_path.stat().st_size > 44:
            return output_path
    except (OSError, subprocess.CalledProcessError) as exception:
        logger.warning("Audio normalization skipped: %s", exception)

    return input_path


def extract_text(result: Any) -> str:
    if isinstance(result, str):
        return result.strip()

    if isinstance(result, dict):
        value = result.get("text") or result.get("transcript")
        if value is not None:
            return str(value).strip()

    return ""


def extract_language(result: Any, text: str) -> str:
    if isinstance(result, dict):
        value = result.get("language")
        if value:
            return str(value)

    configured_language = os.getenv("ASR_LANGUAGE", "").strip()
    if configured_language:
        return configured_language

    return "en" if text else "unknown"


def extract_confidence(result: Any) -> float | None:
    if not isinstance(result, dict):
        return None

    for key in ["confidence", "score"]:
        value = result.get(key)
        if isinstance(value, int | float):
            return clamp_score(float(value))

    chunks = result.get("chunks")
    if not isinstance(chunks, list):
        return None

    scores = [
        clamp_score(float(chunk["score"]))
        for chunk in chunks
        if isinstance(chunk, dict) and isinstance(chunk.get("score"), int | float)
    ]

    return None if not scores else sum(scores) / len(scores)


def build_hf_image_labels(result: Any) -> list[HfImageLabel]:
    best_by_category: dict[str, HfImageLabel] = {}

    for prediction in normalize_hf_predictions(result):
        raw_label = str(prediction.get("label") or prediction.get("name") or "").strip()
        if not raw_label:
            continue

        confidence = clamp_score(float(prediction.get("score") or prediction.get("confidence") or 0.0))
        category = map_image_label_to_category(raw_label)
        label = HfImageLabel(category, round(confidence, 3), raw_label)
        existing = best_by_category.get(category)

        if existing is None or label.confidence > existing.confidence:
            best_by_category[category] = label

    labels = sorted(best_by_category.values(), key=lambda item: item.confidence, reverse=True)
    min_confidence = vision_label_min_confidence()
    confident_labels = [label for label in labels if label.confidence >= min_confidence]

    return (confident_labels or labels[:1])[:5]


def vision_label_min_confidence() -> float:
    try:
        return clamp_score(float(os.getenv("VISION_LABEL_MIN_CONFIDENCE", "0.20")))
    except ValueError:
        return 0.20


def normalize_hf_predictions(result: Any) -> list[dict[str, Any]]:
    if isinstance(result, list):
        if result and isinstance(result[0], list):
            return [item for item in result[0] if isinstance(item, dict)]

        return [item for item in result if isinstance(item, dict)]

    if isinstance(result, dict):
        labels = result.get("labels")
        scores = result.get("scores")

        if isinstance(labels, list) and isinstance(scores, list):
            return [
                {"label": label, "score": score}
                for label, score in zip(labels, scores, strict=False)
            ]

        if "label" in result or "score" in result:
            return [result]

    return []


def map_image_label_to_category(raw_label: str) -> str:
    normalized = raw_label.lower()
    mapped = IMAGE_CATEGORY_LABELS.get(normalized)
    if mapped:
        return mapped

    if contains_any(normalized, "pothole", "road", "asphalt", "sidewalk", "crack", "sinkhole"):
        return "RoadDamage"
    if contains_any(normalized, "flood", "water", "drain"):
        return "Flooding"
    if contains_any(normalized, "streetlight", "traffic signal", "lamp", "light pole"):
        return "Streetlight"
    if contains_any(normalized, "trash", "garbage", "debris", "dumping", "litter"):
        return "Sanitation"
    if contains_any(normalized, "graffiti", "vandal"):
        return "Graffiti"
    if contains_any(normalized, "tree", "branch", "limb"):
        return "TreeHazard"

    return "GeneralIncident"


def clamp_score(value: float) -> float:
    return max(0.0, min(1.0, value))


def resize_embedding(values: list[float], dimensions: int) -> list[float]:
    if len(values) > dimensions:
        resized = values[:dimensions]
    elif len(values) < dimensions:
        resized = values + ([0.0] * (dimensions - len(values)))
    else:
        resized = values

    magnitude = math.sqrt(sum(value * value for value in resized))
    if magnitude == 0:
        return resized

    return [value / magnitude for value in resized]


def baseline_incident_volume_forecast(
    history: list[IncidentVolumeHistoryPoint],
    horizon_days: int,
) -> list[IncidentVolumeForecastPoint]:
    counts_by_date = {
        parsed_date: point.count
        for point in history
        if (parsed_date := parse_iso_date(point.date)) is not None
    }
    end_date = max(counts_by_date.keys(), default=date.today())
    start_date = min(counts_by_date.keys(), default=end_date)
    history_days = max(7, (end_date - start_date).days + 1)
    counts = [
        counts_by_date.get(start_date + timedelta(days=offset), 0)
        for offset in range(history_days)
    ]
    window = min(7, len(counts))
    recent_average = average_last(counts, window)
    previous_average = average_previous(counts, window)
    daily_trend = (recent_average - previous_average) / max(1, window)
    weekday_factors = build_weekday_factors(counts_by_date, max(1.0, recent_average))
    forecast: list[IncidentVolumeForecastPoint] = []

    for offset in range(1, horizon_days + 1):
        forecast_date = end_date + timedelta(days=offset)
        projected = max(0.0, (recent_average + daily_trend * offset) * weekday_factors[forecast_date.weekday()])
        margin = max(1.0, projected * 0.35)
        forecast.append(
            IncidentVolumeForecastPoint(
                date=forecast_date.isoformat(),
                forecastCount=round(projected, 1),
                lowerBound=max(0, math.floor(projected - margin)),
                upperBound=math.ceil(projected + margin),
            )
        )

    return forecast


def parse_iso_date(value: str) -> date | None:
    try:
        return date.fromisoformat(value)
    except ValueError:
        return None


def average_last(values: list[int], window: int) -> float:
    if not values:
        return 0.0

    return sum(values[-window:]) / max(1, min(window, len(values)))


def average_previous(values: list[int], window: int) -> float:
    if len(values) <= window:
        return average_last(values, window)

    previous = values[:-window][-window:]
    return sum(previous) / max(1, len(previous))


def build_weekday_factors(counts_by_date: dict[date, int], fallback_average: float) -> dict[int, float]:
    factors: dict[int, float] = {}
    for weekday in range(7):
        counts = [
            count
            for observed_date, count in counts_by_date.items()
            if observed_date.weekday() == weekday
        ]
        factors[weekday] = 1.0 if not counts else max(0.55, min(1.65, (sum(counts) / len(counts)) / fallback_average))

    return factors


def determine_category(description: str, media: list[IncidentMediaDescriptor]) -> str:
    media_context = " ".join(
        " ".join(
            value
            for value in [
                item.fileName,
                item.analysisSummary or "",
                item.transcript or "",
                " ".join(item.detectedLabels),
            ]
            if value
        ).lower()
        for item in media
    )
    combined = f"{description} {media_context}"

    if contains_any(combined, "pothole", "road crack", "sinkhole", "street damage", "asphalt"):
        return "RoadDamage"
    if contains_any(combined, "flood", "water leak", "standing water", "drain"):
        return "Flooding"
    if contains_any(combined, "streetlight", "traffic light", "signal light", "lamp"):
        return "Streetlight"
    if contains_any(combined, "trash", "debris", "dumping", "garbage"):
        return "Sanitation"
    if contains_any(combined, "graffiti", "vandalism"):
        return "Graffiti"
    if contains_any(combined, "tree", "branch", "fallen limb"):
        return "TreeHazard"

    return "GeneralIncident"


def determine_severity(description: str) -> str:
    if contains_any(description, "injury", "injured", "emergency", "sinkhole", "collapsed"):
        return "Critical"
    if contains_any(description, "large", "dangerous", "blocking", "blocked", "deep", "major"):
        return "High"
    if contains_any(description, "small", "minor", "low"):
        return "Low"

    return "Medium"


def determine_agency(category: str) -> str:
    return {
        "RoadDamage": "DOT",
        "Flooding": "WATER",
        "Streetlight": "UTILITIES",
        "Sanitation": "SANITATION",
        "Graffiti": "PUBLICWORKS",
        "TreeHazard": "PARKS",
    }.get(category, "CITYOPS")


def determine_confidence(category: str, media: list[IncidentMediaDescriptor]) -> float:
    base_confidence = 0.66 if category == "GeneralIncident" else 0.84
    analyzed_media_count = sum(1 for item in media if item.analysisStatus.lower() == "succeeded")
    modality_boost = min(0.12, len(media) * 0.025 + analyzed_media_count * 0.04)
    return round(min(0.97, base_confidence + modality_boost), 2)


def build_summary(category: str, severity: str, agency: str, description: str) -> str:
    trimmed = description.strip()
    if len(trimmed) > 180:
        trimmed = f"{trimmed[:177]}..."

    return f"{severity} {category} report routed to {agency}: {trimmed}"


def build_evidence(
    description: str,
    category: str,
    severity: str,
    agency: str,
    media: list[IncidentMediaDescriptor],
) -> list[EvidenceItem]:
    evidence = [
        EvidenceItem(
            kind="Text",
            title="Incident category signal",
            detail=category_evidence_detail(description, category),
            confidence=0.62 if category == "GeneralIncident" else 0.84,
        ),
        EvidenceItem(
            kind="Text",
            title="Severity signal",
            detail=severity_evidence_detail(description, severity),
            confidence=0.84 if severity in {"High", "Critical"} else 0.72,
        ),
        EvidenceItem(
            kind="Routing",
            title="Agency routing rule",
            detail=f"{category} incidents are routed to {agency}.",
            confidence=0.8,
        ),
    ]

    media_counts = count_modalities(media)
    for modality, count in media_counts.items():
        evidence.append(
            EvidenceItem(
                kind=modality,
                title=f"{modality} evidence attached",
                detail=f"{count} {modality.lower()} item(s) are available for downstream model analysis.",
                confidence=0.65,
            )
        )

    for item in media:
        if item.analysisStatus.lower() != "succeeded":
            continue

        if item.transcript:
            evidence.append(
                EvidenceItem(
                    kind="Audio",
                    title="Audio transcript used",
                    detail=trim_detail(item.transcript),
                    confidence=0.72,
                )
            )

        if item.detectedLabels:
            evidence.append(
                EvidenceItem(
                    kind="Image",
                    title="Image labels used",
                    detail=f"Detected label(s): {', '.join(item.detectedLabels)}.",
                    confidence=0.72,
                )
            )

        if item.analysisSummary and not item.transcript and not item.detectedLabels:
            evidence.append(
                EvidenceItem(
                    kind=item.mediaType,
                    title="Media analysis used",
                    detail=trim_detail(item.analysisSummary),
                    confidence=0.65,
                )
            )

    return evidence


def build_analysis_text(request: IncidentAnalysisRequest) -> str:
    media_context: list[str] = []
    for item in request.media:
        media_context.extend(
            value
            for value in [
                item.fileName,
                item.analysisSummary,
                item.transcript,
                " ".join(item.detectedLabels),
            ]
            if value
        )

    return " ".join([request.description, *media_context])


def infer_audio_transcript(file_name: str) -> str:
    normalized = file_name.lower()
    if contains_any(normalized, "pothole", "road", "crack", "sinkhole"):
        return "Caller reports a large pothole causing drivers to swerve around road damage."
    if contains_any(normalized, "flood", "water", "drain"):
        return "Caller reports standing water and a blocked drain affecting the street."
    if contains_any(normalized, "light", "signal", "lamp"):
        return "Caller reports a streetlight or traffic signal issue."
    if contains_any(normalized, "trash", "debris", "garbage"):
        return "Caller reports trash or debris blocking public space."
    if contains_any(normalized, "tree", "branch", "limb"):
        return "Caller reports a tree branch hazard blocking public space."
    return ""


def trim_detail(value: str) -> str:
    trimmed = value.strip()
    return trimmed if len(trimmed) <= 240 else f"{trimmed[:237]}..."


def category_evidence_detail(description: str, category: str) -> str:
    terms = {
        "RoadDamage": matching_terms(description, "pothole", "road crack", "sinkhole", "street damage", "asphalt"),
        "Flooding": matching_terms(description, "flood", "water leak", "standing water", "drain"),
        "Streetlight": matching_terms(description, "streetlight", "traffic light", "signal light", "lamp"),
        "Sanitation": matching_terms(description, "trash", "debris", "dumping", "garbage"),
        "Graffiti": matching_terms(description, "graffiti", "vandalism"),
        "TreeHazard": matching_terms(description, "tree", "branch", "fallen limb"),
    }.get(category, [])

    return "No specific category keyword was detected." if not terms else f"Matched term(s): {', '.join(terms)}."


def severity_evidence_detail(description: str, severity: str) -> str:
    terms = {
        "Critical": matching_terms(description, "injury", "injured", "emergency", "sinkhole", "collapsed"),
        "High": matching_terms(description, "large", "dangerous", "blocking", "blocked", "deep", "major"),
        "Low": matching_terms(description, "small", "minor", "low"),
    }.get(severity, [])

    return "No high-risk or low-risk keyword was detected." if not terms else f"Matched term(s): {', '.join(terms)}."


def count_modalities(media: list[IncidentMediaDescriptor]) -> dict[str, int]:
    counts: dict[str, int] = {}
    for item in media:
        kind = normalize_media_kind(item.contentType, item.mediaType)
        counts[kind] = counts.get(kind, 0) + 1
    return counts


def normalize_media_kind(content_type: str, media_type: str) -> str:
    normalized_content_type = content_type.lower()
    normalized_media_type = media_type.lower()
    if normalized_content_type.startswith("image/") or normalized_media_type == "image":
        return "Image"
    if normalized_content_type.startswith("audio/") or normalized_media_type == "audio":
        return "Audio"
    if normalized_content_type == "application/pdf" or normalized_media_type == "document":
        return "Document"
    return "Media"


def hash_embedding(text: str, dimensions: int) -> list[float]:
    embedding = [0.0] * dimensions
    for token in tokenize(text):
        add_feature(embedding, token, 1.0)
        add_feature(embedding, f"kind:{normalize_incident_concept(token)}", 0.45)

    magnitude = math.sqrt(sum(value * value for value in embedding))
    if magnitude == 0:
        return embedding

    return [value / magnitude for value in embedding]


def tokenize(text: str) -> list[str]:
    tokens = re.split(r"[\s,.;:!?/\\\-_()[\]]+", text.lower())
    return [
        normalize_token(token.strip())
        for token in tokens
        if len(token.strip()) > 2 and token.strip() not in STOP_WORDS
    ]


def normalize_token(token: str) -> str:
    return {
        "potholes": "pothole",
        "cracks": "crack",
        "streets": "street",
        "roads": "road",
        "blocked": "blocking",
        "swerving": "swerve",
        "garbage": "trash",
        "dumping": "dump",
        "flooded": "flood",
        "flooding": "flood",
        "lights": "light",
        "streetlights": "streetlight",
    }.get(token, token)


def normalize_incident_concept(token: str) -> str:
    if token in {"asphalt", "crack", "pavement", "pothole", "road", "street", "swerve"}:
        return "road-damage"
    if token in {"drain", "flood", "water"}:
        return "flooding"
    if token in {"lamp", "light", "signal", "streetlight"}:
        return "streetlight"
    if token in {"debris", "dump", "trash"}:
        return "sanitation"
    return token


def add_feature(embedding: list[float], feature: str, weight: float) -> None:
    digest = hashlib.sha256(feature.encode("utf-8")).digest()
    index = int.from_bytes(digest[:4], "little") % len(embedding)
    sign = 1 if digest[4] & 1 == 0 else -1
    embedding[index] += sign * weight


def contains_any(value: str, *keywords: str) -> bool:
    return any(keyword in value for keyword in keywords)


def matching_terms(value: str, *keywords: str) -> list[str]:
    return [keyword for keyword in keywords if keyword in value]


def elapsed_ms(started: float) -> int:
    return max(0, round((time.perf_counter() - started) * 1000))
