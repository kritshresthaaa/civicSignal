using CivicSignal.Application.AiEvaluations.Models;

namespace CivicSignal.Application.AiEvaluations;

internal sealed class AiEvaluationService : IAiEvaluationService
{
    public Task<AiEvaluationBaselineReportDto> GetBaselineReportAsync(CancellationToken cancellationToken = default)
    {
        var generatedAt = DateTimeOffset.Parse("2026-07-24T20:18:17+00:00");
        var report = new AiEvaluationBaselineReportDto(
            "CivicSignal deterministic baseline",
            "2026.07.24",
            generatedAt,
            "Repeatable local benchmark for classification, duplicate detection, forecasting, audio, vision, and generated-report quality.",
            FixtureCounts:
            [
                new AiEvaluationFixtureCountDto("Classification cases", 27),
                new AiEvaluationFixtureCountDto("Duplicate queries", 7),
                new AiEvaluationFixtureCountDto("Forecast history days", 21),
                new AiEvaluationFixtureCountDto("Forecast holdout days", 7),
                new AiEvaluationFixtureCountDto("Audio cases", 5),
                new AiEvaluationFixtureCountDto("Image cases", 5),
                new AiEvaluationFixtureCountDto("Generated report cases", 3)
            ],
            MetricGroups:
            [
                new AiEvaluationMetricGroupDto(
                    "Classification",
                    "Category, severity, and agency-routing quality for incident triage.",
                    [
                        HigherIsBetter("Category accuracy", 96.3, "%", 90),
                        HigherIsBetter("Category macro F1", 96.7, "%", 90),
                        HigherIsBetter("Severity accuracy", 96.3, "%", 88),
                        HigherIsBetter("Severity macro F1", 91.0, "%", 85),
                        HigherIsBetter("Agency accuracy", 96.3, "%", 90)
                    ]),
                new AiEvaluationMetricGroupDto(
                    "Duplicate Detection",
                    "pgvector and geospatial duplicate scoring with false-merge control.",
                    [
                        HigherIsBetter("Precision", 100, "%", 92),
                        HigherIsBetter("Recall", 100, "%", 85),
                        HigherIsBetter("F1", 100, "%", 90),
                        HigherIsBetter("Recall@5", 100, "%", 90),
                        LowerIsBetter("False-merge rate", 0, "%", 2)
                    ]),
                new AiEvaluationMetricGroupDto(
                    "Forecasting",
                    "Transparent moving-average/trend workload forecast baseline.",
                    [
                        LowerIsBetter("MAE", 0.95, "count", 2),
                        LowerIsBetter("RMSE", 1.06, "count", 3),
                        LowerIsBetter("MAPE", 3.5, "%", 8)
                    ]),
                new AiEvaluationMetricGroupDto(
                    "Audio",
                    "Speech and language readiness checks for future field audio uploads.",
                    [
                        LowerIsBetter("Word error rate", 13.3, "%", 18),
                        HigherIsBetter("Language accuracy", 100, "%", 95),
                        LowerIsBetter("P95 latency", 1152, "ms", 1600)
                    ]),
                new AiEvaluationMetricGroupDto(
                    "Images",
                    "Vision-label quality gates for road damage and unsupported image cases.",
                    [
                        HigherIsBetter("Precision", 88.9, "%", 85),
                        HigherIsBetter("Recall", 88.9, "%", 82),
                        HigherIsBetter("F1", 88.9, "%", 84),
                        HigherIsBetter("Human agreement", 80, "%", 75),
                        LowerIsBetter("Unsupported-detection rate", 11.1, "%", 15)
                    ]),
                new AiEvaluationMetricGroupDto(
                    "Generated Reports",
                    "Draft-report factuality, required-field coverage, and reviewer acceptance.",
                    [
                        HigherIsBetter("Required-field completion", 93.3, "%", 90),
                        HigherIsBetter("Factual consistency", 93.3, "%", 90),
                        LowerIsBetter("Unsupported-claim rate", 6.2, "%", 8),
                        HigherIsBetter("Reviewer acceptance", 66.7, "%", 60)
                    ])
            ],
            Gates:
            [
                new AiEvaluationGateDto("Category routing gate", "Classification", 96.7, "%", 90, true, true, "Category macro F1 must stay above the auto-routing threshold."),
                new AiEvaluationGateDto("Duplicate safety gate", "Duplicates", 0, "%", 2, false, true, "False merges must remain rare because merging unrelated reports is operationally risky."),
                new AiEvaluationGateDto("Forecast workload gate", "Forecasting", 3.5, "%", 8, false, true, "Forecast MAPE must remain low enough for staffing previews."),
                new AiEvaluationGateDto("Audio intake gate", "Audio", 13.3, "%", 18, false, true, "Audio WER must be controlled before enabling voice-first intake."),
                new AiEvaluationGateDto("Vision review gate", "Images", 88.9, "%", 84, true, true, "Image F1 must clear reviewer-assist quality before model promotion."),
                new AiEvaluationGateDto("Draft factuality gate", "Generated Reports", 6.2, "%", 8, false, true, "Unsupported generated-report claims must stay below the review threshold.")
            ],
            ModelRuns:
            [
                new AiModelRunDto("Local deterministic baseline", "CivicSignal", "2026.07.24", "Passed", generatedAt, "Current repeatable benchmark in evaluation/reports/baseline-results.md."),
                new AiModelRunDto("Hugging Face multimodal service", "Hugging Face", "Planned", "Not connected", null, "Future Python service can compete against the same gates before production use."),
                new AiModelRunDto("OpenAI report evaluator", "OpenAI", "Planned", "Not connected", null, "Optional evaluator for generated-work-order factuality and unsupported-claim checks.")
            ],
            Comparisons:
            [
                new AiEvaluationComparisonDto("Text triage", "Rule-based deterministic classifier", "Hugging Face or OpenAI text model", "Promote only if macro F1 improves without lowering agency accuracy."),
                new AiEvaluationComparisonDto("Duplicate detection", "pgvector text embeddings plus geospatial filter", "Remote embedding model variants", "Promote only if recall improves while false-merge rate remains below 2%."),
                new AiEvaluationComparisonDto("Image analysis", "Fixture-level label agreement checks", "Vision model endpoint", "Promote only after reviewed media samples clear precision and recall gates."),
                new AiEvaluationComparisonDto("Audio intake", "Transcript fixture WER and language checks", "Speech-to-text endpoint", "Promote only if WER and P95 latency stay within gate limits."),
                new AiEvaluationComparisonDto("Forecasting", "Moving-average/trend baseline", "Time-series model", "Promote only if holdout MAPE and RMSE improve.")
            ],
            NextUpgrades:
            [
                "Store evaluation runs, model versions, prompt versions, and gate outcomes in PostgreSQL.",
                "Add reviewed incident-media samples from real uploads.",
                "Compare Hugging Face and OpenAI model runs against the same fixed baseline fixtures.",
                "Add CI quality gates so model changes cannot silently degrade triage or duplicate safety."
            ]);

        return Task.FromResult(report);
    }

    private static AiEvaluationMetricDto HigherIsBetter(
        string name,
        double value,
        string unit,
        double threshold)
    {
        return new AiEvaluationMetricDto(name, value, unit, threshold, true, value >= threshold);
    }

    private static AiEvaluationMetricDto LowerIsBetter(
        string name,
        double value,
        string unit,
        double threshold)
    {
        return new AiEvaluationMetricDto(name, value, unit, threshold, false, value <= threshold);
    }
}
