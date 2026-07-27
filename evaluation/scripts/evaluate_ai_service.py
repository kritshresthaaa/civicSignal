#!/usr/bin/env python3
"""Evaluate a running CivicSignal AI service against fixed fixtures."""

from __future__ import annotations

import argparse
import json
import math
import time
import urllib.error
import urllib.request
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from evaluate_baselines import (
    DATASETS,
    REPORTS,
    bounded_decay,
    classification_metrics,
    evaluate_forecast,
    haversine_meters,
    jaccard,
    levenshtein_distance,
    load_json,
    load_jsonl,
    normalize_label,
    normalize_words,
    pct,
    recency_hours,
    safe_divide,
)


DEFAULT_REPORT_PATH = REPORTS / "ai-service-results.md"
DEFAULT_BASE_URL = "http://localhost:8010"
DEFAULT_EMBEDDING_DIMENSIONS = 384
EMBEDDING_DUPLICATE_THRESHOLD = 0.70


def request_json(base_url: str, path: str, payload: dict[str, Any] | None = None, timeout: float = 30) -> dict[str, Any]:
    body = json.dumps(payload).encode("utf-8") if payload is not None else None
    headers = {"Content-Type": "application/json"} if payload is not None else {}
    request = urllib.request.Request(
        f"{base_url.rstrip('/')}{path}",
        data=body,
        headers=headers,
        method="POST" if payload is not None else "GET",
    )

    with urllib.request.urlopen(request, timeout=timeout) as response:
        return json.loads(response.read().decode("utf-8"))


def request_multipart_file(
    base_url: str,
    path: str,
    file_name: str,
    content_type: str,
    content: bytes,
    timeout: float = 30,
) -> dict[str, Any]:
    boundary = f"----civicsignal-eval-{int(time.time() * 1000)}"
    body = (
        f"--{boundary}\r\n"
        f'Content-Disposition: form-data; name="file"; filename="{file_name}"\r\n'
        f"Content-Type: {content_type}\r\n\r\n"
    ).encode("utf-8") + content + f"\r\n--{boundary}--\r\n".encode("utf-8")
    request = urllib.request.Request(
        f"{base_url.rstrip('/')}{path}",
        data=body,
        headers={"Content-Type": f"multipart/form-data; boundary={boundary}"},
        method="POST",
    )

    with urllib.request.urlopen(request, timeout=timeout) as response:
        return json.loads(response.read().decode("utf-8"))


def evaluate_text_triage(base_url: str, cases: list[dict[str, Any]], timeout: float) -> dict[str, Any]:
    predictions: list[dict[str, Any]] = []
    errors: list[dict[str, str]] = []
    latencies: list[float] = []

    for case in cases:
        payload = {
            "incidentId": case["id"],
            "description": case["text"],
            "latitude": case.get("latitude", 40.7128),
            "longitude": case.get("longitude", -74.0060),
            "media": [],
        }
        started = time.perf_counter()

        try:
            response = request_json(base_url, "/v1/incidents/analyze", payload, timeout)
        except (TimeoutError, urllib.error.URLError, urllib.error.HTTPError) as error:
            errors.append({"id": case["id"], "error": str(error)})
            continue

        latencies.append((time.perf_counter() - started) * 1000)
        predictions.append(
            {
                "id": case["id"],
                "expectedCategory": case["category"],
                "category": response.get("category", "GeneralIncident"),
                "expectedSeverity": case["severity"],
                "severity": response.get("severity", "Medium"),
                "expectedAgency": case["agency"],
                "agency": response.get("suggestedAgencyCode", "CITYOPS"),
                "confidence": float(response.get("confidence") or 0.0),
                "modelName": str(response.get("modelName") or "unknown"),
                "modelVersion": str(response.get("modelVersion") or "unknown"),
                "evidenceCount": len(response.get("evidence") or []),
            }
        )

    category_expected = [row["expectedCategory"] for row in predictions]
    category_predicted = [row["category"] for row in predictions]
    severity_expected = [row["expectedSeverity"] for row in predictions]
    severity_predicted = [row["severity"] for row in predictions]
    agency_expected = [row["expectedAgency"] for row in predictions]
    agency_predicted = [row["agency"] for row in predictions]

    return {
        "cases": len(cases),
        "completed": len(predictions),
        "errors": errors,
        "category": classification_metrics(category_expected, category_predicted),
        "severity": classification_metrics(severity_expected, severity_predicted),
        "agency_accuracy": safe_divide(
            sum(1 for actual, guess in zip(agency_expected, agency_predicted) if actual == guess),
            len(agency_expected),
        ),
        "average_latency_milliseconds": average(latencies),
        "p95_latency_milliseconds": percentile(latencies, 95),
        "average_confidence": average(row["confidence"] for row in predictions),
        "model_names": sorted({row["modelName"] for row in predictions}),
        "model_versions": sorted({row["modelVersion"] for row in predictions}),
        "predictions": predictions,
    }


def evaluate_embedding_duplicates(
    base_url: str,
    cases: list[dict[str, Any]],
    timeout: float,
    dimensions: int,
) -> dict[str, Any]:
    embedding_cache: dict[str, list[float]] = {}
    scores: list[dict[str, Any]] = []
    errors: list[dict[str, str]] = []
    recall_at_5_hits = 0
    duplicate_queries = 0
    embedding_latencies: list[float] = []
    model_names: set[str] = set()
    model_versions: set[str] = set()

    def get_embedding(text: str) -> list[float]:
        if text in embedding_cache:
            return embedding_cache[text]

        started = time.perf_counter()
        response = request_json(
            base_url,
            "/v1/text/embeddings",
            {"text": text, "dimensions": dimensions},
            timeout,
        )
        embedding_latencies.append((time.perf_counter() - started) * 1000)
        model_names.add(str(response.get("modelName") or "unknown"))
        model_versions.add(str(response.get("modelVersion") or "unknown"))
        embedding = [float(value) for value in response["embedding"]]
        embedding_cache[text] = embedding
        return embedding

    for case in cases:
        scored_candidates: list[dict[str, Any]] = []

        try:
            query_embedding = get_embedding(case["text"])
        except (TimeoutError, urllib.error.URLError, urllib.error.HTTPError, KeyError, ValueError) as error:
            errors.append({"id": case["id"], "error": str(error)})
            continue

        for candidate in case["candidates"]:
            try:
                text_score = embedding_similarity(query_embedding, get_embedding(candidate["text"]))
            except (TimeoutError, urllib.error.URLError, urllib.error.HTTPError, KeyError, ValueError) as error:
                errors.append({"id": f"{case['id']}->{candidate['id']}", "error": str(error)})
                continue

            distance = haversine_meters(
                case["latitude"],
                case["longitude"],
                candidate["latitude"],
                candidate["longitude"],
            )
            image_score = jaccard(set(case.get("imageLabels", [])), set(candidate.get("imageLabels", [])))
            geo_score = bounded_decay(distance, best=50, worst=500)
            time_score = bounded_decay(recency_hours(case["createdAt"], candidate["createdAt"]), best=2, worst=168)
            score = (0.50 * text_score) + (0.20 * image_score) + (0.20 * geo_score) + (0.10 * time_score)
            row = {
                "query_id": case["id"],
                "candidate_id": candidate["id"],
                "expected_duplicate": bool(candidate["duplicate"]),
                "predicted_duplicate": score >= EMBEDDING_DUPLICATE_THRESHOLD,
                "score": round(score, 3),
                "text_score": round(text_score, 3),
                "image_score": round(image_score, 3),
                "geo_score": round(geo_score, 3),
                "time_score": round(time_score, 3),
                "distance_meters": round(distance, 1),
            }
            scores.append(row)
            scored_candidates.append(row)

        true_candidate_ids = {row["candidate_id"] for row in scored_candidates if row["expected_duplicate"]}
        if true_candidate_ids:
            duplicate_queries += 1
            top_five = {
                row["candidate_id"]
                for row in sorted(scored_candidates, key=lambda item: item["score"], reverse=True)[:5]
            }
            if true_candidate_ids & top_five:
                recall_at_5_hits += 1

    tp = sum(1 for score in scores if score["expected_duplicate"] and score["predicted_duplicate"])
    fp = sum(1 for score in scores if not score["expected_duplicate"] and score["predicted_duplicate"])
    fn = sum(1 for score in scores if score["expected_duplicate"] and not score["predicted_duplicate"])
    tn = sum(1 for score in scores if not score["expected_duplicate"] and not score["predicted_duplicate"])
    precision = safe_divide(tp, tp + fp)
    recall = safe_divide(tp, tp + fn)

    return {
        "threshold": EMBEDDING_DUPLICATE_THRESHOLD,
        "dimensions": dimensions,
        "total_candidates": len(scores),
        "true_positives": tp,
        "false_positives": fp,
        "false_negatives": fn,
        "true_negatives": tn,
        "precision": precision,
        "recall": recall,
        "f1": safe_divide(2 * precision * recall, precision + recall),
        "recall_at_5": safe_divide(recall_at_5_hits, duplicate_queries),
        "false_merge_rate": safe_divide(fp, tp + fp),
        "average_embedding_latency_milliseconds": average(embedding_latencies),
        "p95_embedding_latency_milliseconds": percentile(embedding_latencies, 95),
        "model_names": sorted(model_names),
        "model_versions": sorted(model_versions),
        "errors": errors,
        "scores": scores,
    }


def evaluate_ai_service_forecast(base_url: str, series: dict[str, Any], timeout: float) -> dict[str, Any]:
    payload = {
        "history": [{"date": item["date"], "count": item["count"]} for item in series["history"]],
        "horizonDays": len(series["holdout"]),
    }
    started = time.perf_counter()
    response = request_json(base_url, "/v1/forecasting/incident-volume", payload, timeout)
    latency = (time.perf_counter() - started) * 1000
    actual = [item["count"] for item in series["holdout"]]
    predicted = [float(item.get("forecastCount") or item.get("predicted") or 0.0) for item in response.get("forecast", [])]
    predicted = predicted[: len(actual)]
    errors = [guess - truth for truth, guess in zip(actual, predicted)]
    absolute_errors = [abs(error) for error in errors]
    squared_errors = [error**2 for error in errors]
    percentage_errors = [abs(error) / truth for truth, error in zip(actual, errors) if truth > 0]

    return {
        "history_days": len(series["history"]),
        "horizon_days": len(series["holdout"]),
        "mae": average(absolute_errors),
        "rmse": math.sqrt(average(squared_errors)),
        "mape": average(percentage_errors),
        "latency_milliseconds": latency,
        "model_name": str(response.get("modelName") or "unknown"),
        "model_version": str(response.get("modelVersion") or "unknown"),
        "forecast": [
            {
                "date": actual_item["date"],
                "actual": actual_item["count"],
                "predicted": predicted[index],
            }
            for index, actual_item in enumerate(series["holdout"][: len(predicted)])
        ],
    }


def evaluate_audio_media(base_url: str, cases: list[dict[str, Any]], timeout: float) -> dict[str, Any]:
    total_edits = 0
    total_reference_words = 0
    language_matches = 0
    latencies: list[float] = []
    rows: list[dict[str, Any]] = []
    errors: list[dict[str, str]] = []
    model_names: set[str] = set()
    model_versions: set[str] = set()

    for case in cases:
        file_path = DATASETS.parent / str(case["filePath"])
        file_name = file_path.name
        content_type = str(case.get("contentType") or "audio/wav")
        started = time.perf_counter()

        try:
            response = request_multipart_file(
                base_url,
                "/v1/audio/transcriptions",
                file_name,
                content_type,
                file_path.read_bytes(),
                timeout,
            )
        except (TimeoutError, urllib.error.URLError, urllib.error.HTTPError, OSError) as error:
            errors.append({"id": str(case["id"]), "error": str(error)})
            continue

        latency = (time.perf_counter() - started) * 1000
        reference_words = normalize_words(str(case["referenceTranscript"]))
        predicted_text = str(response.get("text") or "")
        predicted_words = normalize_words(predicted_text)
        edits = levenshtein_distance(reference_words, predicted_words)
        language_match = str(case.get("expectedLanguage") or "") == str(response.get("language") or "")
        total_edits += edits
        total_reference_words += len(reference_words)
        language_matches += int(language_match)
        latencies.append(latency)
        model_names.add(str(response.get("modelName") or "unknown"))
        model_versions.add(str(response.get("modelVersion") or "unknown"))
        rows.append(
            {
                "id": case["id"],
                "file": str(case["filePath"]),
                "word_error_rate": safe_divide(edits, len(reference_words)),
                "reference": case["referenceTranscript"],
                "predicted": predicted_text,
                "language_match": language_match,
                "latency_milliseconds": latency,
            }
        )

    return {
        "status": "ok" if not errors else "partial",
        "cases": len(cases),
        "completed": len(rows),
        "errors": errors,
        "word_error_rate": safe_divide(total_edits, total_reference_words),
        "language_accuracy": safe_divide(language_matches, len(rows)),
        "average_latency_milliseconds": average(latencies),
        "p95_latency_milliseconds": percentile(latencies, 95),
        "model_names": sorted(model_names),
        "model_versions": sorted(model_versions),
        "rows": rows,
    }


def evaluate_image_media(base_url: str, cases: list[dict[str, Any]], timeout: float) -> dict[str, Any]:
    tp = fp = fn = 0
    accepted = 0
    unsupported_predictions = 0
    total_predictions = 0
    latencies: list[float] = []
    rows: list[dict[str, Any]] = []
    errors: list[dict[str, str]] = []
    model_names: set[str] = set()
    model_versions: set[str] = set()

    for case in cases:
        file_path = DATASETS.parent / str(case["filePath"])
        file_name = file_path.name
        content_type = str(case.get("contentType") or "image/png")
        started = time.perf_counter()

        try:
            response = request_multipart_file(
                base_url,
                "/v1/images/analyze",
                file_name,
                content_type,
                file_path.read_bytes(),
                timeout,
            )
        except (TimeoutError, urllib.error.URLError, urllib.error.HTTPError, OSError) as error:
            errors.append({"id": str(case["id"]), "error": str(error)})
            continue

        latency = (time.perf_counter() - started) * 1000
        expected = {normalize_label(str(label)) for label in case["expectedLabels"]}
        predicted_labels = [
            str(item.get("name") or item.get("label") or "")
            for item in response.get("labels", [])
            if isinstance(item, dict)
        ]
        predicted = {normalize_label(label) for label in predicted_labels if label}
        unsupported = predicted - expected
        accepted_prediction = bool(expected & predicted) and not unsupported
        tp += len(expected & predicted)
        fp += len(unsupported)
        fn += len(expected - predicted)
        accepted += int(accepted_prediction)
        unsupported_predictions += len(unsupported)
        total_predictions += len(predicted)
        latencies.append(latency)
        model_names.add(str(response.get("modelName") or "unknown"))
        model_versions.add(str(response.get("modelVersion") or "unknown"))
        rows.append(
            {
                "id": case["id"],
                "file": str(case["filePath"]),
                "expected": sorted(expected),
                "predicted": sorted(predicted),
                "unsupported": sorted(unsupported),
                "accepted": accepted_prediction,
                "latency_milliseconds": latency,
            }
        )

    precision = safe_divide(tp, tp + fp)
    recall = safe_divide(tp, tp + fn)

    return {
        "status": "ok" if not errors else "partial",
        "cases": len(cases),
        "completed": len(rows),
        "errors": errors,
        "precision": precision,
        "recall": recall,
        "f1": safe_divide(2 * precision * recall, precision + recall),
        "human_agreement_rate": safe_divide(accepted, len(rows)),
        "unsupported_detection_rate": safe_divide(unsupported_predictions, total_predictions),
        "average_latency_milliseconds": average(latencies),
        "p95_latency_milliseconds": percentile(latencies, 95),
        "model_names": sorted(model_names),
        "model_versions": sorted(model_versions),
        "true_positives": tp,
        "false_positives": fp,
        "false_negatives": fn,
        "rows": rows,
    }


def evaluate_media_contracts(base_url: str, timeout: float) -> dict[str, Any]:
    return {
        "audio": evaluate_audio_media(base_url, load_json(DATASETS / "audio_cases.json"), timeout),
        "image": evaluate_image_media(base_url, load_json(DATASETS / "image_cases.json"), timeout),
    }


def embedding_similarity(left: list[float], right: list[float]) -> float:
    dot = sum(left_value * right_value for left_value, right_value in zip(left, right, strict=False))
    left_norm = math.sqrt(sum(value * value for value in left))
    right_norm = math.sqrt(sum(value * value for value in right))
    if left_norm == 0 or right_norm == 0:
        return 0.0

    cosine = dot / (left_norm * right_norm)
    return max(0.0, min(1.0, (cosine + 1) / 2))


def run_ai_service_evaluation(
    base_url: str,
    timeout: float,
    embedding_dimensions: int,
    skip_media: bool,
) -> dict[str, Any]:
    classification_cases = load_jsonl(DATASETS / "classification_cases.jsonl")
    duplicate_cases = load_json(DATASETS / "duplicate_cases.json")
    forecast_series = load_json(DATASETS / "incident_volume.json")
    baseline_forecast = evaluate_forecast(forecast_series)

    health = request_json(base_url, "/health", timeout=timeout)

    return {
        "generated_at": datetime.now(timezone.utc).isoformat(timespec="seconds"),
        "endpoint": base_url.rstrip("/"),
        "health": health,
        "fixtures": {
            "classification_cases": len(classification_cases),
            "duplicate_queries": len(duplicate_cases),
            "forecast_history_days": len(forecast_series["history"]),
            "forecast_holdout_days": len(forecast_series["holdout"]),
        },
        "text_triage": evaluate_text_triage(base_url, classification_cases, timeout),
        "embedding_duplicates": evaluate_embedding_duplicates(base_url, duplicate_cases, timeout, embedding_dimensions),
        "forecasting": evaluate_ai_service_forecast(base_url, forecast_series, timeout),
        "baseline_forecasting": baseline_forecast,
        "media_contracts": evaluate_media_contracts(base_url, timeout) if not skip_media else skipped_media_contracts(),
    }


def skipped_media_contracts() -> dict[str, Any]:
    return {
        "audio": {
            "status": "skipped",
            "cases": 0,
            "completed": 0,
            "word_error_rate": 0,
            "language_accuracy": 0,
            "average_latency_milliseconds": 0,
            "p95_latency_milliseconds": 0,
            "model_names": ["n/a"],
            "model_versions": ["n/a"],
            "note": "Skipped for this run. Use reviewed media fixtures before claiming WER or vision precision.",
        },
        "image": {
            "status": "skipped",
            "cases": 0,
            "completed": 0,
            "precision": 0,
            "recall": 0,
            "f1": 0,
            "human_agreement_rate": 0,
            "unsupported_detection_rate": 0,
            "average_latency_milliseconds": 0,
            "p95_latency_milliseconds": 0,
            "model_names": ["n/a"],
            "model_versions": ["n/a"],
            "note": "Skipped for this run. Use reviewed media fixtures before claiming WER or vision precision.",
        },
    }


def render_report(results: dict[str, Any]) -> str:
    health = results["health"]
    text = results["text_triage"]
    category = text["category"]
    severity = text["severity"]
    duplicates = results["embedding_duplicates"]
    forecasting = results["forecasting"]
    baseline_forecasting = results["baseline_forecasting"]
    media_contracts = results["media_contracts"]
    hf = health.get("huggingFace") or {}
    dependencies = hf.get("dependencies") or {}

    lines = [
        "# CivicSignal AI Service Evaluation",
        "",
        f"Generated: `{results['generated_at']}`",
        f"Endpoint: `{results['endpoint']}`",
        f"Runtime mode: `{health.get('mode', 'unknown')}`",
        "",
        "This report evaluates the running AI service contract. In deterministic mode it proves the integration boundary; with `USE_HF_MODELS=true` and optional dependencies installed, the same report becomes the Hugging Face model comparison.",
        "",
        "## Runtime Readiness",
        "",
        "| Item | Value |",
        "| --- | --- |",
        f"| Service | `{health.get('service', 'unknown')}` |",
        f"| Hugging Face enabled | `{bool(hf.get('enabled', False))}` |",
        f"| Dependencies ready | `{bool(hf.get('dependenciesReady', False))}` |",
    ]

    for name, ready in dependencies.items():
        lines.append(f"| Dependency: {name} | `{bool(ready)}` |")

    lines.extend(
        [
            "",
            "## Text Triage Metrics",
            "",
            "| Target | Completed | Accuracy | Macro precision | Macro recall | Macro F1 |",
            "| --- | ---: | ---: | ---: | ---: | ---: |",
            f"| Category | {text['completed']}/{text['cases']} | {pct(category['accuracy'])} | {pct(category['macro_precision'])} | {pct(category['macro_recall'])} | {pct(category['macro_f1'])} |",
            f"| Severity | {text['completed']}/{text['cases']} | {pct(severity['accuracy'])} | {pct(severity['macro_precision'])} | {pct(severity['macro_recall'])} | {pct(severity['macro_f1'])} |",
            f"| Agency routing | {text['completed']}/{text['cases']} | {pct(text['agency_accuracy'])} | n/a | n/a | n/a |",
            "",
            "| Avg latency | P95 latency | Avg confidence | Models |",
            "| ---: | ---: | ---: | --- |",
            f"| {text['average_latency_milliseconds']:.0f} ms | {text['p95_latency_milliseconds']:.0f} ms | {pct(text['average_confidence'])} | {', '.join(f'`{name}`' for name in text['model_names'])} |",
            "",
            "## Embedding Duplicate Metrics",
            "",
            "| Dimensions | Threshold | Precision | Recall | F1 | Recall@5 | False-merge rate | TP | FP | FN | TN |",
            "| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |",
            f"| {duplicates['dimensions']} | {duplicates['threshold']:.2f} | {pct(duplicates['precision'])} | {pct(duplicates['recall'])} | {pct(duplicates['f1'])} | {pct(duplicates['recall_at_5'])} | {pct(duplicates['false_merge_rate'])} | {duplicates['true_positives']} | {duplicates['false_positives']} | {duplicates['false_negatives']} | {duplicates['true_negatives']} |",
            "",
            "| Avg embedding latency | P95 embedding latency | Embedding models |",
            "| ---: | ---: | --- |",
            f"| {duplicates['average_embedding_latency_milliseconds']:.0f} ms | {duplicates['p95_embedding_latency_milliseconds']:.0f} ms | {', '.join(f'`{name}`' for name in duplicates['model_names'])} |",
            "",
            "## Forecasting Metrics",
            "",
            "| Model | Horizon | MAE | RMSE | MAPE | Latency |",
            "| --- | ---: | ---: | ---: | ---: | ---: |",
            f"| AI service `{forecasting['model_name']}` | {forecasting['horizon_days']} days | {forecasting['mae']:.2f} | {forecasting['rmse']:.2f} | {pct(forecasting['mape'])} | {forecasting['latency_milliseconds']:.0f} ms |",
            f"| Local baseline | {baseline_forecasting['horizon_days']} days | {baseline_forecasting['mae']:.2f} | {baseline_forecasting['rmse']:.2f} | {pct(baseline_forecasting['mape'])} | n/a |",
            "",
            "## Audio Media Metrics",
            "",
            "| Status | Completed | WER | Language accuracy | Avg latency | P95 latency | Models | Versions |",
            "| --- | ---: | ---: | ---: | ---: | ---: | --- | --- |",
            f"| {media_contracts['audio']['status']} | {media_contracts['audio']['completed']}/{media_contracts['audio']['cases']} | {pct(media_contracts['audio']['word_error_rate'])} | {pct(media_contracts['audio']['language_accuracy'])} | {media_contracts['audio']['average_latency_milliseconds']:.0f} ms | {media_contracts['audio']['p95_latency_milliseconds']:.0f} ms | {', '.join(f'`{name}`' for name in media_contracts['audio']['model_names'])} | {', '.join(f'`{version}`' for version in media_contracts['audio']['model_versions'])} |",
            "",
            "## Image Media Metrics",
            "",
            "| Status | Completed | Precision | Recall | F1 | Human agreement | Unsupported rate | Models | Versions |",
            "| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- |",
            f"| {media_contracts['image']['status']} | {media_contracts['image']['completed']}/{media_contracts['image']['cases']} | {pct(media_contracts['image']['precision'])} | {pct(media_contracts['image']['recall'])} | {pct(media_contracts['image']['f1'])} | {pct(media_contracts['image']['human_agreement_rate'])} | {pct(media_contracts['image']['unsupported_detection_rate'])} | {', '.join(f'`{name}`' for name in media_contracts['image']['model_names'])} | {', '.join(f'`{version}`' for version in media_contracts['image']['model_versions'])} |",
        ]
    )

    append_media_breakdowns(lines, media_contracts)

    lines.extend(
        [
            "",
            "## Promotion Notes",
            "",
            "- Treat deterministic runs as integration proof, not final model quality.",
            "- Promote a Hugging Face run only when text macro-F1, duplicate F1, WER, image F1, and forecasting error beat or match the deterministic baseline on reviewed fixtures.",
            "- Keep media fixtures reviewed and versioned before claiming WER or visual precision from real model inference.",
            "- Record model names, model versions, latency, and unsupported-claim behavior before using numbers on a resume or LinkedIn post.",
        ]
    )

    return "\n".join(lines) + "\n"


def append_media_breakdowns(lines: list[str], media_contracts: dict[str, Any]) -> None:
    audio = media_contracts["audio"]
    image = media_contracts["image"]

    if audio.get("rows"):
        lines.extend(
            [
                "",
                "## Audio Fixture Breakdown",
                "",
                "| Id | WER | Language match | Latency | Predicted transcript |",
                "| --- | ---: | --- | ---: | --- |",
            ]
        )
        for row in audio["rows"]:
            lines.append(
                f"| {row['id']} | {pct(row['word_error_rate'])} | `{bool(row['language_match'])}` | {row['latency_milliseconds']:.0f} ms | {table_text(row['predicted'])} |"
            )

    if image.get("rows"):
        lines.extend(
            [
                "",
                "## Image Fixture Breakdown",
                "",
                "| Id | Accepted | Expected | Predicted | Unsupported | Latency |",
                "| --- | --- | --- | --- | --- | ---: |",
            ]
        )
        for row in image["rows"]:
            lines.append(
                f"| {row['id']} | `{bool(row['accepted'])}` | {table_text(', '.join(row['expected']))} | {table_text(', '.join(row['predicted']))} | {table_text(', '.join(row['unsupported']))} | {row['latency_milliseconds']:.0f} ms |"
            )


def table_text(value: object, limit: int = 100) -> str:
    text = str(value).replace("|", "\\|").replace("\n", " ").strip()
    if not text:
        return "`empty`"

    return text if len(text) <= limit else f"{text[: limit - 3]}..."


def console_summary(results: dict[str, Any]) -> dict[str, Any]:
    text = results["text_triage"]
    duplicates = results["embedding_duplicates"]
    forecasting = results["forecasting"]
    media = results["media_contracts"]

    return {
        "mode": results["health"].get("mode"),
        "text_triage": {
            "category_accuracy": round(text["category"]["accuracy"], 4),
            "category_macro_f1": round(text["category"]["macro_f1"], 4),
            "agency_accuracy": round(text["agency_accuracy"], 4),
            "p95_latency_milliseconds": round(text["p95_latency_milliseconds"], 1),
        },
        "embedding_duplicates": {
            "precision": round(duplicates["precision"], 4),
            "recall": round(duplicates["recall"], 4),
            "f1": round(duplicates["f1"], 4),
            "false_merge_rate": round(duplicates["false_merge_rate"], 4),
        },
        "forecasting": {
            "mae": round(forecasting["mae"], 4),
            "rmse": round(forecasting["rmse"], 4),
            "mape": round(forecasting["mape"], 4),
        },
        "audio_media": {
            "status": media["audio"]["status"],
            "completed": media["audio"]["completed"],
            "word_error_rate": round(media["audio"]["word_error_rate"], 4),
            "language_accuracy": round(media["audio"]["language_accuracy"], 4),
            "p95_latency_milliseconds": round(media["audio"]["p95_latency_milliseconds"], 1),
        },
        "image_media": {
            "status": media["image"]["status"],
            "completed": media["image"]["completed"],
            "precision": round(media["image"]["precision"], 4),
            "recall": round(media["image"]["recall"], 4),
            "f1": round(media["image"]["f1"], 4),
            "unsupported_detection_rate": round(media["image"]["unsupported_detection_rate"], 4),
        },
    }


def average(values: Any) -> float:
    sequence = list(values)
    return sum(sequence) / len(sequence) if sequence else 0.0


def percentile(values: list[float], percentile_value: float) -> float:
    if not values:
        return 0.0

    ordered = sorted(values)
    index = (len(ordered) - 1) * (percentile_value / 100)
    lower = math.floor(index)
    upper = math.ceil(index)
    if lower == upper:
        return ordered[int(index)]

    weight = index - lower
    return ordered[lower] * (1 - weight) + ordered[upper] * weight


def main() -> None:
    parser = argparse.ArgumentParser(description="Evaluate a running CivicSignal AI service.")
    parser.add_argument("--base-url", default=DEFAULT_BASE_URL, help="AI service base URL.")
    parser.add_argument("--timeout", type=float, default=30, help="Per-request timeout in seconds.")
    parser.add_argument("--embedding-dimensions", type=int, default=DEFAULT_EMBEDDING_DIMENSIONS)
    parser.add_argument("--write-report", action="store_true", help="Write Markdown report to evaluation/reports.")
    parser.add_argument("--report-path", type=Path, default=DEFAULT_REPORT_PATH)
    parser.add_argument(
        "--skip-media",
        action="store_true",
        help="Skip reviewed audio/image calls to avoid large ASR/vision cold-start downloads.",
    )
    args = parser.parse_args()

    results = run_ai_service_evaluation(args.base_url, args.timeout, args.embedding_dimensions, args.skip_media)
    print(json.dumps(console_summary(results), indent=2))

    if args.write_report:
        args.report_path.parent.mkdir(parents=True, exist_ok=True)
        args.report_path.write_text(render_report(results), encoding="utf-8")
        print(f"Wrote {args.report_path}")


if __name__ == "__main__":
    main()
