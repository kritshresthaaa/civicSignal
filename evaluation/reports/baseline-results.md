# CivicSignal AI Baseline Evaluation

Generated: `2026-07-25T02:58:18+00:00`

This report evaluates deterministic local baselines against fixed fixtures. It is the repeatable benchmark for future Hugging Face, pgvector, and forecasting model changes.

## Fixture Counts

| Fixture | Count |
| --- | ---: |
| Classification cases | 27 |
| Duplicate queries | 7 |
| Forecast history days | 21 |
| Forecast holdout days | 7 |
| Audio cases | 5 |
| Image cases | 5 |
| Generated report cases | 3 |

## Classification Metrics

| Target | Accuracy | Macro precision | Macro recall | Macro F1 |
| --- | ---: | ---: | ---: | ---: |
| Category | 96.3% | 97.6% | 96.4% | 96.7% |
| Severity | 96.3% | 87.5% | 98.6% | 91.0% |
| Agency routing | 96.3% | n/a | n/a | n/a |

## Category Confusion Matrix

| Actual \ Predicted | Flooding | GeneralIncident | Graffiti | RoadDamage | Sanitation | Streetlight | TreeHazard |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Flooding | 4 | 0 | 0 | 0 | 0 | 0 | 0 |
| GeneralIncident | 0 | 3 | 0 | 0 | 0 | 0 | 0 |
| Graffiti | 0 | 0 | 3 | 0 | 0 | 0 | 0 |
| RoadDamage | 0 | 0 | 0 | 5 | 0 | 0 | 0 |
| Sanitation | 0 | 0 | 0 | 0 | 4 | 0 | 0 |
| Streetlight | 0 | 0 | 0 | 0 | 0 | 4 | 0 |
| TreeHazard | 0 | 0 | 0 | 1 | 0 | 0 | 3 |

## Duplicate Detection Metrics

| Threshold | Precision | Recall | F1 | Recall@5 | False-merge rate | TP | FP | FN | TN |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 0.70 | 100.0% | 100.0% | 100.0% | 100.0% | 0.0% | 6 | 0 | 0 | 14 |

## Forecasting Metrics

| Horizon | MAE | RMSE | MAPE |
| ---: | ---: | ---: | ---: |
| 7 days | 0.95 | 1.06 | 3.5% |

| Date | Actual | Predicted |
| --- | ---: | ---: |
| 2026-07-14 | 24 | 22.63 |
| 2026-07-15 | 26 | 24.40 |
| 2026-07-16 | 28 | 26.59 |
| 2026-07-17 | 31 | 30.74 |
| 2026-07-18 | 35 | 35.76 |
| 2026-07-19 | 34 | 34.60 |
| 2026-07-20 | 22 | 22.67 |

## Audio Metrics

| Cases | WER | Language accuracy | Avg latency | P95 latency |
| ---: | ---: | ---: | ---: | ---: |
| 5 | 13.3% | 100.0% | 1001 ms | 1152 ms |

## Image Metrics

| Cases | Precision | Recall | F1 | Human agreement | Unsupported-detection rate |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 5 | 100.0% | 100.0% | 100.0% | 100.0% | 0.0% |

## Generated Report Metrics

| Cases | Required-field completion | Factual consistency | Unsupported-claim rate | Reviewer acceptance |
| ---: | ---: | ---: | ---: | ---: |
| 3 | 93.3% | 93.3% | 6.2% | 66.7% |

## Interpretation

- Classification measures category, severity, and agency routing quality.
- Duplicate metrics separate false merges from missed duplicates because false merges are operationally risky.
- Forecasting currently uses a transparent moving-average/trend baseline; future models should beat this report before replacing it.
- Audio, image, and generated-report checks are fixture-level quality gates until larger reviewed datasets are imported.

## Next Evaluation Upgrades

- Add real historical NYC 311 holdout data once imports are populated.
- Store model version, prompt version, and evaluation run metadata in a database table.
- Replace fixture-level audio and image expected outputs with reviewed media samples.
