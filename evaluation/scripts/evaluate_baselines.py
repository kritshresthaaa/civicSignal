#!/usr/bin/env python3
"""Evaluate CivicSignal AI baseline behavior against fixed fixtures."""

from __future__ import annotations

import argparse
import json
import math
import re
from collections import defaultdict
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[1]
DATASETS = ROOT / "datasets"
REPORTS = ROOT / "reports"

DUPLICATE_THRESHOLD = 0.70

CATEGORY_KEYWORDS = (
    ("RoadDamage", ("pothole", "road crack", "sinkhole", "street damage", "asphalt", "pavement", "curb ramp", "sidewalk crack")),
    ("Flooding", ("flood", "standing water", "water leak", "storm drain", "drain grate", "pooling", "clogged drain")),
    ("Streetlight", ("streetlight", "traffic light", "traffic signal", "lamp pole", "lamp")),
    ("Sanitation", ("trash", "garbage", "debris", "dumping", "dumped", "litter", "furniture")),
    ("Graffiti", ("graffiti", "spray paint", "tags", "vandalism")),
    ("TreeHazard", ("tree branch", "tree limb", "fallen tree", "broken branch", "roots", "limb")),
)

AGENCY_BY_CATEGORY = {
    "RoadDamage": "DOT",
    "Flooding": "WATER",
    "Streetlight": "UTILITIES",
    "Sanitation": "SANITATION",
    "Graffiti": "PUBLICWORKS",
    "TreeHazard": "PARKS",
    "GeneralIncident": "CITYOPS",
}

STOPWORDS = {
    "a",
    "about",
    "after",
    "and",
    "are",
    "around",
    "at",
    "by",
    "for",
    "from",
    "has",
    "in",
    "is",
    "near",
    "of",
    "on",
    "the",
    "to",
    "with",
}

TOKEN_SYNONYMS = {
    "big": "large",
    "blocked": "blocking",
    "clogged": "blocked",
    "crack": "damage",
    "cracked": "damage",
    "dumped": "dumping",
    "garbage": "trash",
    "lamp": "streetlight",
    "limb": "branch",
    "paint": "graffiti",
    "pavement": "road",
    "potholes": "pothole",
    "spray": "graffiti",
    "swerve": "swerving",
    "tags": "graffiti",
}


@dataclass(frozen=True)
class CandidateScore:
    query_id: str
    candidate_id: str
    expected_duplicate: bool
    predicted_duplicate: bool
    score: float
    text_score: float
    image_score: float
    geo_score: float
    time_score: float
    distance_meters: float


def load_jsonl(path: Path) -> list[dict[str, Any]]:
    with path.open("r", encoding="utf-8") as source:
        return [json.loads(line) for line in source if line.strip()]


def load_json(path: Path) -> Any:
    with path.open("r", encoding="utf-8") as source:
        return json.load(source)


def predict_category(text: str) -> str:
    lower_text = text.lower()
    for category, keywords in CATEGORY_KEYWORDS:
        if any(keyword in lower_text for keyword in keywords):
            return category

    return "GeneralIncident"


def predict_severity(text: str) -> str:
    lower_text = text.lower()
    if any(term in lower_text for term in ("injury", "injured", "emergency", "sinkhole", "collapsed", "collapsing")):
        return "Critical"

    if any(term in lower_text for term in ("large", "dangerous", "blocking", "blocked", "deep", "major", "both lanes", "sparking")):
        return "High"

    if any(term in lower_text for term in ("small", "minor", "low")):
        return "Low"

    return "Medium"


def predict_agency(category: str) -> str:
    return AGENCY_BY_CATEGORY.get(category, "CITYOPS")


def classification_metrics(expected: list[str], predicted: list[str]) -> dict[str, Any]:
    labels = sorted(set(expected) | set(predicted))
    total = len(expected)
    correct = sum(1 for actual, guess in zip(expected, predicted) if actual == guess)
    per_label: dict[str, dict[str, float]] = {}
    confusion: dict[str, dict[str, int]] = {label: {inner: 0 for inner in labels} for label in labels}

    for actual, guess in zip(expected, predicted):
        confusion[actual][guess] += 1

    for label in labels:
        tp = sum(1 for actual, guess in zip(expected, predicted) if actual == label and guess == label)
        fp = sum(1 for actual, guess in zip(expected, predicted) if actual != label and guess == label)
        fn = sum(1 for actual, guess in zip(expected, predicted) if actual == label and guess != label)
        precision = safe_divide(tp, tp + fp)
        recall = safe_divide(tp, tp + fn)
        per_label[label] = {
            "precision": precision,
            "recall": recall,
            "f1": safe_divide(2 * precision * recall, precision + recall),
            "support": sum(1 for actual in expected if actual == label),
        }

    return {
        "accuracy": safe_divide(correct, total),
        "macro_precision": average([metrics["precision"] for metrics in per_label.values()]),
        "macro_recall": average([metrics["recall"] for metrics in per_label.values()]),
        "macro_f1": average([metrics["f1"] for metrics in per_label.values()]),
        "per_label": per_label,
        "confusion_matrix": confusion,
        "labels": labels,
        "total": total,
    }


def evaluate_classification(cases: list[dict[str, Any]]) -> dict[str, Any]:
    category_expected = [case["category"] for case in cases]
    category_predicted = [predict_category(case["text"]) for case in cases]
    severity_expected = [case["severity"] for case in cases]
    severity_predicted = [predict_severity(case["text"]) for case in cases]
    agency_expected = [case["agency"] for case in cases]
    agency_predicted = [predict_agency(category) for category in category_predicted]

    agency_correct = sum(1 for actual, guess in zip(agency_expected, agency_predicted) if actual == guess)

    return {
        "category": classification_metrics(category_expected, category_predicted),
        "severity": classification_metrics(severity_expected, severity_predicted),
        "agency_accuracy": safe_divide(agency_correct, len(cases)),
        "predictions": [
            {
                "id": case["id"],
                "category": category_predicted[index],
                "severity": severity_predicted[index],
                "agency": agency_predicted[index],
            }
            for index, case in enumerate(cases)
        ],
    }


def tokenize(text: str) -> set[str]:
    tokens = re.findall(r"[a-z0-9]+", text.lower())
    normalized = [TOKEN_SYNONYMS.get(token, token) for token in tokens if token not in STOPWORDS]
    return {token for token in normalized if len(token) > 1}


def jaccard(left: set[str], right: set[str]) -> float:
    if not left and not right:
        return 0.0

    return len(left & right) / len(left | right)


def haversine_meters(lat1: float, lon1: float, lat2: float, lon2: float) -> float:
    radius_meters = 6_371_000
    phi1 = math.radians(lat1)
    phi2 = math.radians(lat2)
    delta_phi = math.radians(lat2 - lat1)
    delta_lambda = math.radians(lon2 - lon1)
    a = math.sin(delta_phi / 2) ** 2 + math.cos(phi1) * math.cos(phi2) * math.sin(delta_lambda / 2) ** 2
    return 2 * radius_meters * math.atan2(math.sqrt(a), math.sqrt(1 - a))


def recency_hours(left: str, right: str) -> float:
    left_dt = datetime.fromisoformat(left.replace("Z", "+00:00"))
    right_dt = datetime.fromisoformat(right.replace("Z", "+00:00"))
    return abs((left_dt - right_dt).total_seconds()) / 3600


def bounded_decay(value: float, best: float, worst: float) -> float:
    if value <= best:
        return 1.0
    if value >= worst:
        return 0.0
    return 1 - ((value - best) / (worst - best))


def score_duplicate(query: dict[str, Any], candidate: dict[str, Any]) -> CandidateScore:
    text_score = jaccard(tokenize(query["text"]), tokenize(candidate["text"]))
    image_score = jaccard(set(query.get("imageLabels", [])), set(candidate.get("imageLabels", [])))
    distance = haversine_meters(
        query["latitude"],
        query["longitude"],
        candidate["latitude"],
        candidate["longitude"],
    )
    geo_score = bounded_decay(distance, best=50, worst=500)
    time_score = bounded_decay(recency_hours(query["createdAt"], candidate["createdAt"]), best=2, worst=168)
    score = (0.50 * text_score) + (0.20 * image_score) + (0.20 * geo_score) + (0.10 * time_score)

    return CandidateScore(
        query_id=query["id"],
        candidate_id=candidate["id"],
        expected_duplicate=bool(candidate["duplicate"]),
        predicted_duplicate=score >= DUPLICATE_THRESHOLD,
        score=round(score, 3),
        text_score=round(text_score, 3),
        image_score=round(image_score, 3),
        geo_score=round(geo_score, 3),
        time_score=round(time_score, 3),
        distance_meters=round(distance, 1),
    )


def evaluate_duplicates(cases: list[dict[str, Any]]) -> dict[str, Any]:
    scores: list[CandidateScore] = []
    recall_at_5_hits = 0
    duplicate_queries = 0

    for case in cases:
        scored_candidates = [score_duplicate(case, candidate) for candidate in case["candidates"]]
        scores.extend(scored_candidates)
        true_candidate_ids = {score.candidate_id for score in scored_candidates if score.expected_duplicate}
        if true_candidate_ids:
            duplicate_queries += 1
            top_five = {score.candidate_id for score in sorted(scored_candidates, key=lambda item: item.score, reverse=True)[:5]}
            if true_candidate_ids & top_five:
                recall_at_5_hits += 1

    tp = sum(1 for score in scores if score.expected_duplicate and score.predicted_duplicate)
    fp = sum(1 for score in scores if not score.expected_duplicate and score.predicted_duplicate)
    fn = sum(1 for score in scores if score.expected_duplicate and not score.predicted_duplicate)
    tn = sum(1 for score in scores if not score.expected_duplicate and not score.predicted_duplicate)
    precision = safe_divide(tp, tp + fp)
    recall = safe_divide(tp, tp + fn)

    return {
        "threshold": DUPLICATE_THRESHOLD,
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
        "scores": [score.__dict__ for score in scores],
    }


def moving_average_forecast(history: list[dict[str, Any]], horizon: int) -> list[dict[str, Any]]:
    counts = [item["count"] for item in history]
    weekly_baseline = average(counts[-7:])
    previous_weekly_baseline = average(counts[-14:-7])
    daily_trend = (weekly_baseline - previous_weekly_baseline) / 7
    weekday_averages = weekday_baselines(history)
    forecast = []

    for index in range(horizon):
        reference = history[-7 + index % 7]
        weekday_factor = safe_divide(weekday_averages[reference["weekday"]], average(weekday_averages.values()))
        predicted = max(0, (weekly_baseline + (daily_trend * (index + 1))) * weekday_factor)
        forecast.append({"date": reference["date"], "predicted": round(predicted, 2)})

    return forecast


def weekday_baselines(history: list[dict[str, Any]]) -> dict[int, float]:
    buckets: dict[int, list[int]] = defaultdict(list)
    for item in history:
        parsed = datetime.fromisoformat(item["date"])
        item["weekday"] = parsed.weekday()
        buckets[item["weekday"]].append(item["count"])

    overall = average(item["count"] for item in history)
    return {weekday: average(buckets.get(weekday, [overall])) for weekday in range(7)}


def evaluate_forecast(series: dict[str, Any]) -> dict[str, Any]:
    forecast = moving_average_forecast(series["history"], len(series["holdout"]))
    actual = [item["count"] for item in series["holdout"]]
    predicted = [item["predicted"] for item in forecast]
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
        "forecast": [
            {
                "date": actual_item["date"],
                "actual": actual_item["count"],
                "predicted": predicted_item["predicted"],
            }
            for actual_item, predicted_item in zip(series["holdout"], forecast)
        ],
    }


def evaluate_audio(cases: list[dict[str, Any]]) -> dict[str, Any]:
    total_edits = 0
    total_reference_words = 0
    language_matches = 0
    latencies = []
    rows = []

    for case in cases:
        reference_words = normalize_words(case["referenceTranscript"])
        predicted_words = normalize_words(case["predictedTranscript"])
        edits = levenshtein_distance(reference_words, predicted_words)
        total_edits += edits
        total_reference_words += len(reference_words)
        language_matches += int(case["expectedLanguage"] == case["predictedLanguage"])
        latencies.append(case["latencyMilliseconds"])
        rows.append(
            {
                "id": case["id"],
                "word_error_rate": safe_divide(edits, len(reference_words)),
                "latency_milliseconds": case["latencyMilliseconds"],
                "language_match": case["expectedLanguage"] == case["predictedLanguage"],
            }
        )

    return {
        "cases": len(cases),
        "word_error_rate": safe_divide(total_edits, total_reference_words),
        "language_accuracy": safe_divide(language_matches, len(cases)),
        "average_latency_milliseconds": average(latencies),
        "p95_latency_milliseconds": percentile(latencies, 95),
        "rows": rows,
    }


def evaluate_images(cases: list[dict[str, Any]]) -> dict[str, Any]:
    tp = fp = fn = 0
    accepted = 0
    unsupported_predictions = 0
    total_predictions = 0

    for case in cases:
        expected = {normalize_label(label) for label in case["expectedLabels"]}
        predicted = {normalize_label(label) for label in case["predictedLabels"]}
        unsupported = {normalize_label(label) for label in case.get("unsupportedPredictions", [])}
        tp += len(expected & predicted)
        fp += len(predicted - expected)
        fn += len(expected - predicted)
        accepted += int(bool(case["humanAccepted"]))
        unsupported_predictions += len(unsupported)
        total_predictions += len(predicted)

    precision = safe_divide(tp, tp + fp)
    recall = safe_divide(tp, tp + fn)

    return {
        "cases": len(cases),
        "precision": precision,
        "recall": recall,
        "f1": safe_divide(2 * precision * recall, precision + recall),
        "human_agreement_rate": safe_divide(accepted, len(cases)),
        "unsupported_detection_rate": safe_divide(unsupported_predictions, total_predictions),
        "true_positives": tp,
        "false_positives": fp,
        "false_negatives": fn,
    }


def evaluate_generated_reports(cases: list[dict[str, Any]]) -> dict[str, Any]:
    required_fields = 0
    completed_fields = 0
    expected_facts = 0
    supported_facts = 0
    unsupported_claims = 0
    reviewer_accepted = 0

    for case in cases:
        report = case["report"]
        report_text = case["reportText"].lower()
        for field in case["requiredFields"]:
            required_fields += 1
            completed_fields += int(bool(str(report.get(field, "")).strip()))

        for fact in case["expectedFacts"]:
            expected_facts += 1
            supported_facts += int(fact.lower() in report_text)

        unsupported_claims += len(case.get("unsupportedClaims", []))
        reviewer_accepted += int(bool(case["reviewerAccepted"]))

    return {
        "cases": len(cases),
        "required_field_completion": safe_divide(completed_fields, required_fields),
        "factual_consistency": safe_divide(supported_facts, expected_facts),
        "unsupported_claim_rate": safe_divide(unsupported_claims, expected_facts + unsupported_claims),
        "reviewer_acceptance_rate": safe_divide(reviewer_accepted, len(cases)),
        "completed_fields": completed_fields,
        "required_fields": required_fields,
        "supported_facts": supported_facts,
        "expected_facts": expected_facts,
        "unsupported_claims": unsupported_claims,
    }


def run_evaluation() -> dict[str, Any]:
    classification_cases = load_jsonl(DATASETS / "classification_cases.jsonl")
    duplicate_cases = load_json(DATASETS / "duplicate_cases.json")
    forecast_series = load_json(DATASETS / "incident_volume.json")
    audio_cases = load_json(DATASETS / "audio_cases.json")
    image_cases = load_json(DATASETS / "image_cases.json")
    generated_report_cases = load_json(DATASETS / "generated_report_cases.json")

    return {
        "generated_at": datetime.now(timezone.utc).isoformat(timespec="seconds"),
        "fixtures": {
            "classification_cases": len(classification_cases),
            "duplicate_queries": len(duplicate_cases),
            "forecast_history_days": len(forecast_series["history"]),
            "forecast_holdout_days": len(forecast_series["holdout"]),
            "audio_cases": len(audio_cases),
            "image_cases": len(image_cases),
            "generated_report_cases": len(generated_report_cases),
        },
        "classification": evaluate_classification(classification_cases),
        "duplicates": evaluate_duplicates(duplicate_cases),
        "forecasting": evaluate_forecast(forecast_series),
        "audio": evaluate_audio(audio_cases),
        "images": evaluate_images(image_cases),
        "generated_reports": evaluate_generated_reports(generated_report_cases),
    }


def render_report(results: dict[str, Any]) -> str:
    classification = results["classification"]
    category = classification["category"]
    severity = classification["severity"]
    duplicates = results["duplicates"]
    forecasting = results["forecasting"]
    audio = results["audio"]
    images = results["images"]
    generated_reports = results["generated_reports"]

    lines = [
        "# CivicSignal AI Baseline Evaluation",
        "",
        f"Generated: `{results['generated_at']}`",
        "",
        "This report evaluates deterministic local baselines against fixed fixtures. It is the repeatable benchmark for future Hugging Face, pgvector, and forecasting model changes.",
        "",
        "## Fixture Counts",
        "",
        "| Fixture | Count |",
        "| --- | ---: |",
        f"| Classification cases | {results['fixtures']['classification_cases']} |",
        f"| Duplicate queries | {results['fixtures']['duplicate_queries']} |",
        f"| Forecast history days | {results['fixtures']['forecast_history_days']} |",
        f"| Forecast holdout days | {results['fixtures']['forecast_holdout_days']} |",
        f"| Audio cases | {results['fixtures']['audio_cases']} |",
        f"| Image cases | {results['fixtures']['image_cases']} |",
        f"| Generated report cases | {results['fixtures']['generated_report_cases']} |",
        "",
        "## Classification Metrics",
        "",
        "| Target | Accuracy | Macro precision | Macro recall | Macro F1 |",
        "| --- | ---: | ---: | ---: | ---: |",
        f"| Category | {pct(category['accuracy'])} | {pct(category['macro_precision'])} | {pct(category['macro_recall'])} | {pct(category['macro_f1'])} |",
        f"| Severity | {pct(severity['accuracy'])} | {pct(severity['macro_precision'])} | {pct(severity['macro_recall'])} | {pct(severity['macro_f1'])} |",
        f"| Agency routing | {pct(classification['agency_accuracy'])} | n/a | n/a | n/a |",
        "",
        "## Category Confusion Matrix",
        "",
        confusion_table(category["labels"], category["confusion_matrix"]),
        "",
        "## Duplicate Detection Metrics",
        "",
        "| Threshold | Precision | Recall | F1 | Recall@5 | False-merge rate | TP | FP | FN | TN |",
        "| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |",
        f"| {duplicates['threshold']:.2f} | {pct(duplicates['precision'])} | {pct(duplicates['recall'])} | {pct(duplicates['f1'])} | {pct(duplicates['recall_at_5'])} | {pct(duplicates['false_merge_rate'])} | {duplicates['true_positives']} | {duplicates['false_positives']} | {duplicates['false_negatives']} | {duplicates['true_negatives']} |",
        "",
        "## Forecasting Metrics",
        "",
        "| Horizon | MAE | RMSE | MAPE |",
        "| ---: | ---: | ---: | ---: |",
        f"| {forecasting['horizon_days']} days | {forecasting['mae']:.2f} | {forecasting['rmse']:.2f} | {pct(forecasting['mape'])} |",
        "",
        "| Date | Actual | Predicted |",
        "| --- | ---: | ---: |",
    ]

    for row in forecasting["forecast"]:
        lines.append(f"| {row['date']} | {row['actual']} | {row['predicted']:.2f} |")

    lines.extend(
        [
            "",
            "## Audio Metrics",
            "",
            "| Cases | WER | Language accuracy | Avg latency | P95 latency |",
            "| ---: | ---: | ---: | ---: | ---: |",
            f"| {audio['cases']} | {pct(audio['word_error_rate'])} | {pct(audio['language_accuracy'])} | {audio['average_latency_milliseconds']:.0f} ms | {audio['p95_latency_milliseconds']:.0f} ms |",
            "",
            "## Image Metrics",
            "",
            "| Cases | Precision | Recall | F1 | Human agreement | Unsupported-detection rate |",
            "| ---: | ---: | ---: | ---: | ---: | ---: |",
            f"| {images['cases']} | {pct(images['precision'])} | {pct(images['recall'])} | {pct(images['f1'])} | {pct(images['human_agreement_rate'])} | {pct(images['unsupported_detection_rate'])} |",
            "",
            "## Generated Report Metrics",
            "",
            "| Cases | Required-field completion | Factual consistency | Unsupported-claim rate | Reviewer acceptance |",
            "| ---: | ---: | ---: | ---: | ---: |",
            f"| {generated_reports['cases']} | {pct(generated_reports['required_field_completion'])} | {pct(generated_reports['factual_consistency'])} | {pct(generated_reports['unsupported_claim_rate'])} | {pct(generated_reports['reviewer_acceptance_rate'])} |",
            "",
            "## Interpretation",
            "",
            "- Classification measures category, severity, and agency routing quality.",
            "- Duplicate metrics separate false merges from missed duplicates because false merges are operationally risky.",
            "- Forecasting currently uses a transparent moving-average/trend baseline; future models should beat this report before replacing it.",
            "- Audio, image, and generated-report checks are fixture-level quality gates until larger reviewed datasets are imported.",
            "",
            "## Next Evaluation Upgrades",
            "",
            "- Add real historical NYC 311 holdout data once imports are populated.",
            "- Store model version, prompt version, and evaluation run metadata in a database table.",
            "- Replace fixture-level audio and image expected outputs with reviewed media samples.",
        ]
    )

    return "\n".join(lines) + "\n"


def confusion_table(labels: list[str], matrix: dict[str, dict[str, int]]) -> str:
    header = "| Actual \\ Predicted | " + " | ".join(labels) + " |"
    divider = "| --- | " + " | ".join("---:" for _ in labels) + " |"
    rows = [header, divider]
    for actual in labels:
        values = " | ".join(str(matrix[actual][predicted]) for predicted in labels)
        rows.append(f"| {actual} | {values} |")
    return "\n".join(rows)


def pct(value: float) -> str:
    return f"{value * 100:.1f}%"


def normalize_words(text: str) -> list[str]:
    return re.findall(r"[a-z0-9]+", text.lower())


def normalize_label(label: str) -> str:
    return re.sub(r"[^a-z0-9]", "", label.lower())


def levenshtein_distance(left: list[str], right: list[str]) -> int:
    previous = list(range(len(right) + 1))
    for left_index, left_word in enumerate(left, start=1):
        current = [left_index]
        for right_index, right_word in enumerate(right, start=1):
            current.append(
                min(
                    current[right_index - 1] + 1,
                    previous[right_index] + 1,
                    previous[right_index - 1] + int(left_word != right_word),
                )
            )
        previous = current

    return previous[-1]


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


def safe_divide(numerator: float, denominator: float) -> float:
    return numerator / denominator if denominator else 0.0


def average(values: Any) -> float:
    sequence = list(values)
    return sum(sequence) / len(sequence) if sequence else 0.0


def console_summary(results: dict[str, Any]) -> dict[str, Any]:
    return {
        "classification": {
            "category_accuracy": round(results["classification"]["category"]["accuracy"], 4),
            "category_macro_f1": round(results["classification"]["category"]["macro_f1"], 4),
            "severity_accuracy": round(results["classification"]["severity"]["accuracy"], 4),
            "severity_macro_f1": round(results["classification"]["severity"]["macro_f1"], 4),
            "agency_accuracy": round(results["classification"]["agency_accuracy"], 4),
        },
        "duplicates": {
            "precision": round(results["duplicates"]["precision"], 4),
            "recall": round(results["duplicates"]["recall"], 4),
            "f1": round(results["duplicates"]["f1"], 4),
            "recall_at_5": round(results["duplicates"]["recall_at_5"], 4),
            "false_merge_rate": round(results["duplicates"]["false_merge_rate"], 4),
        },
        "forecasting": {
            "mae": round(results["forecasting"]["mae"], 4),
            "rmse": round(results["forecasting"]["rmse"], 4),
            "mape": round(results["forecasting"]["mape"], 4),
        },
        "audio": {
            "word_error_rate": round(results["audio"]["word_error_rate"], 4),
            "language_accuracy": round(results["audio"]["language_accuracy"], 4),
            "p95_latency_milliseconds": round(results["audio"]["p95_latency_milliseconds"], 1),
        },
        "images": {
            "precision": round(results["images"]["precision"], 4),
            "recall": round(results["images"]["recall"], 4),
            "human_agreement_rate": round(results["images"]["human_agreement_rate"], 4),
            "unsupported_detection_rate": round(results["images"]["unsupported_detection_rate"], 4),
        },
        "generated_reports": {
            "required_field_completion": round(results["generated_reports"]["required_field_completion"], 4),
            "factual_consistency": round(results["generated_reports"]["factual_consistency"], 4),
            "unsupported_claim_rate": round(results["generated_reports"]["unsupported_claim_rate"], 4),
            "reviewer_acceptance_rate": round(results["generated_reports"]["reviewer_acceptance_rate"], 4),
        },
    }


def main() -> None:
    parser = argparse.ArgumentParser(description="Evaluate CivicSignal AI baseline fixtures.")
    parser.add_argument("--write-report", action="store_true", help="Write Markdown report to evaluation/reports.")
    parser.add_argument("--report-path", type=Path, default=REPORTS / "baseline-results.md")
    args = parser.parse_args()

    results = run_evaluation()
    print(json.dumps(console_summary(results), indent=2))

    if args.write_report:
        args.report_path.parent.mkdir(parents=True, exist_ok=True)
        args.report_path.write_text(render_report(results), encoding="utf-8")
        print(f"Wrote {args.report_path}")


if __name__ == "__main__":
    main()
