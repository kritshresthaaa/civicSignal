# CivicSignal AI Service Evaluation

Generated: `2026-07-25T18:36:58+00:00`
Endpoint: `http://127.0.0.1:8010`
Runtime mode: `huggingface-ready`

This report evaluates the running AI service contract. In deterministic mode it proves the integration boundary; with `USE_HF_MODELS=true` and optional dependencies installed, the same report becomes the Hugging Face model comparison.

## Runtime Readiness

| Item | Value |
| --- | --- |
| Service | `civicsignal-ai-service` |
| Hugging Face enabled | `True` |
| Dependencies ready | `True` |
| Dependency: transformers | `True` |
| Dependency: sentenceTransformers | `True` |
| Dependency: torch | `True` |
| Dependency: pillow | `True` |
| Dependency: sentencePiece | `True` |
| Dependency: protobuf | `True` |

## Text Triage Metrics

| Target | Completed | Accuracy | Macro precision | Macro recall | Macro F1 |
| --- | ---: | ---: | ---: | ---: | ---: |
| Category | 27/27 | 92.6% | 94.3% | 92.4% | 92.7% |
| Severity | 27/27 | 96.3% | 87.5% | 98.6% | 91.0% |
| Agency routing | 27/27 | 92.6% | n/a | n/a | n/a |

| Avg latency | P95 latency | Avg confidence | Models |
| ---: | ---: | ---: | --- |
| 1606 ms | 1770 ms | 80.1% | `facebook/bart-large-mnli` |

## Embedding Duplicate Metrics

| Dimensions | Threshold | Precision | Recall | F1 | Recall@5 | False-merge rate | TP | FP | FN | TN |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1024 | 0.70 | 54.5% | 100.0% | 70.6% | 100.0% | 45.5% | 6 | 5 | 0 | 9 |

| Avg embedding latency | P95 embedding latency | Embedding models |
| ---: | ---: | --- |
| 92 ms | 37 ms | `sentence-transformers/all-MiniLM-L6-v2` |

## Forecasting Metrics

| Model | Horizon | MAE | RMSE | MAPE | Latency |
| --- | ---: | ---: | ---: | ---: | ---: |
| AI service `civicsignal-ai-service-moving-average-trend` | 7 days | 2.91 | 3.02 | 10.4% | 3 ms |
| Local baseline | 7 days | 0.95 | 1.06 | 3.5% | n/a |

## Audio Media Metrics

| Status | Completed | WER | Language accuracy | Avg latency | P95 latency | Models | Versions |
| --- | ---: | ---: | ---: | ---: | ---: | --- | --- |
| ok | 5/5 | 11.1% | 100.0% | 1422 ms | 2825 ms | `openai/whisper-tiny` | `huggingface` |

## Image Media Metrics

| Status | Completed | Precision | Recall | F1 | Human agreement | Unsupported rate | Models | Versions |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- |
| ok | 5/5 | 50.0% | 60.0% | 54.5% | 60.0% | 50.0% | `openai/clip-vit-base-patch32` | `huggingface` |

## Audio Fixture Breakdown

| Id | WER | Language match | Latency | Predicted transcript |
| --- | ---: | --- | ---: | --- |
| aud-001 | 23.1% | `True` | 3271 ms | There is a large pot hole near the school crosswalk and cars are swirving. |
| aud-002 | 0.0% | `True` | 942 ms | Water is pooling around the blocked storm drain. |
| aud-003 | 28.6% | `True` | 892 ms | The street light outside my apartment is out. |
| aud-004 | 0.0% | `True` | 1041 ms | trash bags were dumped behind the grocery store overnight. |
| aud-005 | 0.0% | `True` | 962 ms | A tree branch is hanging over the playground. |

## Image Fixture Breakdown

| Id | Accepted | Expected | Predicted | Unsupported | Latency |
| --- | --- | --- | --- | --- | ---: |
| img-001 | `True` | roaddamage | roaddamage | `empty` | 394 ms |
| img-002 | `False` | flooding | generalincident, roaddamage | generalincident, roaddamage | 201 ms |
| img-003 | `True` | streetlight | streetlight | `empty` | 274 ms |
| img-004 | `False` | sanitation | streetlight | streetlight | 194 ms |
| img-005 | `True` | treehazard | treehazard | `empty` | 268 ms |

## Promotion Notes

- Treat deterministic runs as integration proof, not final model quality.
- Promote a Hugging Face run only when text macro-F1, duplicate F1, WER, image F1, and forecasting error beat or match the deterministic baseline on reviewed fixtures.
- Keep media fixtures reviewed and versioned before claiming WER or visual precision from real model inference.
- Record model names, model versions, latency, and unsupported-claim behavior before using numbers on a resume or LinkedIn post.
