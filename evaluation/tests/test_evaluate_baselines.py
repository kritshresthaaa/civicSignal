from __future__ import annotations

import importlib.util
import sys
import unittest
from pathlib import Path


MODULE_PATH = Path(__file__).resolve().parents[1] / "scripts" / "evaluate_baselines.py"
SPEC = importlib.util.spec_from_file_location("evaluate_baselines", MODULE_PATH)
assert SPEC is not None
evaluate_baselines = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
sys.modules[SPEC.name] = evaluate_baselines
SPEC.loader.exec_module(evaluate_baselines)


class EvaluateBaselinesTests(unittest.TestCase):
    def test_classification_metrics_scores_perfect_predictions(self) -> None:
        metrics = evaluate_baselines.classification_metrics(
            ["RoadDamage", "Flooding", "Flooding"],
            ["RoadDamage", "Flooding", "Flooding"],
        )

        self.assertEqual(metrics["accuracy"], 1)
        self.assertEqual(metrics["macro_f1"], 1)
        self.assertEqual(metrics["confusion_matrix"]["Flooding"]["Flooding"], 2)

    def test_duplicate_score_prefers_near_semantic_match(self) -> None:
        query = {
            "id": "query",
            "text": "Large pothole near Pine Street and 3rd Avenue.",
            "latitude": 40.7128,
            "longitude": -74.006,
            "createdAt": "2026-07-20T10:00:00Z",
            "imageLabels": ["RoadDamage"],
        }
        matching_candidate = {
            "id": "match",
            "text": "Big pothole by Pine Street and 3rd Avenue.",
            "latitude": 40.71281,
            "longitude": -74.00601,
            "createdAt": "2026-07-20T09:30:00Z",
            "imageLabels": ["RoadDamage"],
            "duplicate": True,
        }
        different_candidate = {
            "id": "different",
            "text": "Streetlight out in another neighborhood.",
            "latitude": 40.732,
            "longitude": -74.026,
            "createdAt": "2026-07-20T09:30:00Z",
            "imageLabels": ["Streetlight"],
            "duplicate": False,
        }

        match_score = evaluate_baselines.score_duplicate(query, matching_candidate)
        different_score = evaluate_baselines.score_duplicate(query, different_candidate)

        self.assertGreater(match_score.score, different_score.score)
        self.assertTrue(match_score.predicted_duplicate)
        self.assertFalse(different_score.predicted_duplicate)

    def test_forecast_evaluation_returns_error_metrics(self) -> None:
        results = evaluate_baselines.run_evaluation()
        forecasting = results["forecasting"]

        self.assertEqual(forecasting["horizon_days"], 7)
        self.assertGreaterEqual(forecasting["mae"], 0)
        self.assertGreaterEqual(forecasting["rmse"], forecasting["mae"])
        self.assertGreaterEqual(forecasting["mape"], 0)

    def test_audio_evaluation_computes_word_error_rate(self) -> None:
        metrics = evaluate_baselines.evaluate_audio(
            [
                {
                    "id": "audio",
                    "referenceTranscript": "large pothole near main street",
                    "predictedTranscript": "large pothole on main street",
                    "expectedLanguage": "en",
                    "predictedLanguage": "en",
                    "latencyMilliseconds": 1000,
                }
            ]
        )

        self.assertAlmostEqual(metrics["word_error_rate"], 0.2)
        self.assertEqual(metrics["language_accuracy"], 1)
        self.assertEqual(metrics["p95_latency_milliseconds"], 1000)

    def test_image_evaluation_tracks_unsupported_predictions(self) -> None:
        metrics = evaluate_baselines.evaluate_images(
            [
                {
                    "id": "image",
                    "expectedLabels": ["RoadDamage"],
                    "predictedLabels": ["RoadDamage", "Flooding"],
                    "humanAccepted": False,
                    "unsupportedPredictions": ["Flooding"],
                }
            ]
        )

        self.assertAlmostEqual(metrics["precision"], 0.5)
        self.assertEqual(metrics["recall"], 1)
        self.assertAlmostEqual(metrics["unsupported_detection_rate"], 0.5)

    def test_generated_report_evaluation_tracks_completion_and_claims(self) -> None:
        metrics = evaluate_baselines.evaluate_generated_reports(
            [
                {
                    "id": "report",
                    "requiredFields": ["incidentType", "agency"],
                    "report": {"incidentType": "RoadDamage", "agency": ""},
                    "expectedFacts": ["RoadDamage", "DOT"],
                    "reportText": "RoadDamage report.",
                    "unsupportedClaims": ["Crew dispatched."],
                    "reviewerAccepted": False,
                }
            ]
        )

        self.assertEqual(metrics["required_field_completion"], 0.5)
        self.assertEqual(metrics["factual_consistency"], 0.5)
        self.assertAlmostEqual(metrics["unsupported_claim_rate"], 1 / 3)

    def test_full_evaluation_has_required_sections(self) -> None:
        results = evaluate_baselines.run_evaluation()

        self.assertIn("classification", results)
        self.assertIn("duplicates", results)
        self.assertIn("forecasting", results)
        self.assertIn("audio", results)
        self.assertIn("images", results)
        self.assertIn("generated_reports", results)
        self.assertEqual(results["fixtures"]["classification_cases"], 27)
        self.assertEqual(results["fixtures"]["audio_cases"], 5)
        self.assertEqual(results["fixtures"]["image_cases"], 5)
        self.assertEqual(results["fixtures"]["generated_report_cases"], 3)
        self.assertGreater(results["duplicates"]["total_candidates"], 0)


if __name__ == "__main__":
    unittest.main()
