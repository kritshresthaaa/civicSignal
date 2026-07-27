# CivicSignal AI

CivicSignal AI is a backend-first scaffold for a multimodal city incident triage platform. The .NET backend uses Clean Architecture with simple application services, PostgreSQL/PostGIS/pgvector persistence, Redis caching, RabbitMQ incident processing, S3-compatible object storage, Identity auth, Swagger, media upload, evidence-backed AI-assisted triage, duplicate detection, geocoding, weather context, controlled agent workflow, forecasting, human review corrections, and a background processing worker. A Python FastAPI AI service provides a Hugging Face-ready inference boundary for triage, embeddings, and forecasting.

## Repository Layout

```text
src/
  CivicSignal.Api/             ASP.NET Core Web API
  CivicSignal.Application/     Application services, DTOs, validators, and ports
  CivicSignal.Domain/          Entities, value objects, domain rules
  CivicSignal.Infrastructure/  Persistence, storage, messaging, AI adapters
  CivicSignal.Worker/          Background processing host

tests/
  CivicSignal.Domain.Tests/
  CivicSignal.Application.Tests/
  CivicSignal.Api.IntegrationTests/
  CivicSignal.Infrastructure.IntegrationTests/
  CivicSignal.Architecture.Tests/

docs/
  architecture/
  decisions/

infra/
  docker/
  postgres/

frontend/      Next.js PWA and operations dashboard
ai-service/    Python FastAPI/Hugging Face-ready inference service
evaluation/    AI quality fixtures, metrics scripts, and baseline reports
```

## Backend Architecture

Dependency direction:

```text
Api + Worker -> Application -> Domain
Infrastructure -> Application -> Domain
```

`Domain` does not depend on any other project. `Application` depends only on `Domain`. `Infrastructure` implements interfaces defined by `Application`.

Persistence uses EF Core repositories behind `Application` abstractions. Shared CRUD behavior lives in `IGenericRepository<TEntity>`, while incident-specific lookup behavior stays in `IIncidentRepository`.

Access management uses ASP.NET Core Identity in `Infrastructure` with role/policy constants in `Application`. Login issues a short-lived JWT access token and a rotating refresh token. Browser clients receive both as HttpOnly cookies, while Swagger/API clients can still use the returned `accessToken` as a bearer token. Public users can report incidents. Staff-only review, processing updates, and analysis use authorization policies.

Incident text is embedded into a 1024-d vector when an incident is created. Duplicate detection uses pgvector cosine distance, PostGIS radius filtering, and time proximity before storing duplicate candidates.

Triage predictions store model metadata and supporting evidence. Each prediction can include model version, prompt version, processing time, text evidence, media evidence, routing evidence, and duplicate-candidate evidence.

The controlled agent workflow runs a fixed tool sequence over stored backend data: understand complaint, collect evidence, get weather, search nearby cases, retrieve routing policy, predict agency, calculate SLA risk, and create a draft work order only when confidence and policy checks pass. It is exposed at `POST /api/incidents/{incidentId}/agent-workflow` and writes `AgentTool` evidence to the latest prediction.

AI evaluation baselines are exposed to staff clients at `GET /api/ai-evaluations/baselines`. The response mirrors the repeatable reports in `evaluation/` with fixture counts, metric groups, pass/fail gates, model-run readiness, and promotion rules for future Hugging Face/OpenAI model comparisons.

Model Lab is exposed at `POST /api/model-lab/analyze` and in the frontend at `/public/model-lab` and `/admin/model-lab`. It shows a transparent baseline text classifier with tokenization, stable token IDs, hashing-trick embedding features, logits, softmax probabilities, evidence terms, category prediction, agency routing, severity, and confidence.

Human review stores both the latest correction summary and an audit history. Reviewers can accept or reject the AI prediction, correct category, agency, and severity, mark a duplicate incident, and record notes for model feedback.

## Local Commands

```bash
/Users/kritshrestha/.dotnet/dotnet sln CivicSignal.slnx list
/Users/kritshrestha/.dotnet/dotnet restore CivicSignal.slnx
/Users/kritshrestha/.dotnet/dotnet build CivicSignal.slnx
/Users/kritshrestha/.dotnet/dotnet test CivicSignal.slnx
/Users/kritshrestha/.dotnet/dotnet tool restore
docker compose up -d postgres
/Users/kritshrestha/.dotnet/dotnet ef database update --project src/CivicSignal.Infrastructure/CivicSignal.Infrastructure.csproj --startup-project src/CivicSignal.Api/CivicSignal.Api.csproj
python3 evaluation/scripts/evaluate_baselines.py --write-report
python3 -m unittest discover -s evaluation/tests
```

Set `RUN_POSTGRES_TESTCONTAINERS=true` before `dotnet test` to run the PostgreSQL/PostGIS/pgvector container integration test.

When the API is running in Development, Swagger UI is available at `http://localhost:5020/swagger`.

## Backend Demo Flow

Fast Docker path:

```bash
cp .env.example .env
docker compose up --build
```

This starts PostgreSQL, Redis, RabbitMQ, MinIO/S3 object storage, the Python AI service, the API, the background worker, and the Next.js frontend. The AI service uses lightweight deterministic mode by default so the full stack starts without downloading Hugging Face/PyTorch model dependencies. The API applies EF migrations, seeds local Identity users, and inserts demo incidents. Open `http://localhost:3000` for the app and `http://localhost:5020/swagger` for Swagger.

Phone/LAN testing:

1. Keep your phone and laptop on the same Wi-Fi.
2. Find the laptop IP, for example `ifconfig | grep "inet 192.168"`.
3. Open `http://<laptop-ip>:3000` on the phone, for example `http://192.168.1.134:3000`.

The frontend uses same-origin API, SignalR hub, media, and health routes by default. That means phones and tablets only need the frontend URL; Next.js proxies backend traffic to the API container.

Ngrok testing:

```bash
docker compose up --build
ngrok http 3000
```

Open the `https://...ngrok-free.app` URL on your phone. This is the best local sharing mode for PWA features because it gives the app an HTTPS origin. Do not expose PostgreSQL, RabbitMQ, Redis, MinIO, or the Python AI service directly.

Reset local test incidents while keeping staff users and imported 311 context:

```bash
docker compose exec -T postgres psql -U postgres -d civic_signal < infra/postgres/reset-local-incidents.sql
```

Local SDK path:

From the repository root:

```bash
docker compose up -d postgres
/Users/kritshrestha/.dotnet/dotnet tool restore
/Users/kritshrestha/.dotnet/dotnet ef database update --project src/CivicSignal.Infrastructure/CivicSignal.Infrastructure.csproj --startup-project src/CivicSignal.Api/CivicSignal.Api.csproj
/Users/kritshrestha/.dotnet/dotnet run --project src/CivicSignal.Api/CivicSignal.Api.csproj
```

In another terminal, start the frontend on the network:

```bash
cd frontend
npm run dev:web
```

Use `POST /api/auth/login` with:

```text
operator@civicsignal.local
Operator123456!
```

For Swagger, copy the returned `accessToken`, click `Authorize`, and paste the token. The Next.js frontend uses the HttpOnly cookies automatically and calls `POST /api/auth/refresh` when a protected request returns 401. Create two nearby similar incidents, call `POST /api/incidents/{incidentId}/analyze`, then inspect `GET /api/incidents/{incidentId}/similar`.

Docker demo data is opt-in through `DEMO_DATA_ENABLED=true`. Seeded incidents are prefixed with `[DEMO]` and include triage predictions, evidence, duplicate candidates, processing steps, and a reviewed correction.

Health endpoints:

```text
GET /health
GET /health/ready
```

System/runtime metadata endpoints:

```text
GET /api/system/capabilities
GET /api/system/integrations
GET /api/system/runtime-policy
```

The frontend dashboard, settings, and admin shell use these API routes plus live incident/search endpoints instead of hard-coded integration status.

Initial auth endpoints:

```text
GET  /api/auth/csrf
POST /api/auth/register
POST /api/auth/login
POST /api/auth/refresh
POST /api/auth/logout
GET  /api/auth/me
```

Newly registered users receive the `Reporter` role. Incident reporting remains public; staff-only review and operations endpoints should use the existing `IncidentReview` and `IncidentOperations` policies as those workflows are added.

Browser clients use HttpOnly auth cookies plus `X-CSRF-TOKEN` for protected staff/admin unsafe requests. Public writes, login, refresh, logout, and bearer-token API calls do not need a CSRF preflight. Swagger and API clients can continue using `Authorization: Bearer <accessToken>` without a CSRF header.

The API also applies baseline security headers and ASP.NET Core rate limiting. Auth endpoints use a stricter limit than general API traffic, and citizen write endpoints use a public-write limit.

Protected staff workflow:

```text
POST /api/incidents/{incidentId}/review
GET  /api/incidents/{incidentId}/reviews
```

Allowed roles: `Administrator`, `Operator`, `Reviewer`. Supported decisions: `Approved`, `Rejected`, `NeedsMoreInfo`.

Review requests can include:

```json
{
  "decision": "Approved",
  "note": "Prediction corrected by reviewer.",
  "correctedCategory": "RoadDamage",
  "correctedAgencyCode": "DOT",
  "correctedSeverity": "High",
  "duplicateOfIncidentId": null,
  "acceptedPrediction": false
}
```

Processing status workflow:

```text
GET  /api/incidents/{incidentId}/status
POST /api/incidents/{incidentId}/processing-status
```

Status reads are public. Updates require `Administrator` or `Operator`. Supported processing statuses: `InProgress`, `Succeeded`, `Failed`.

AI and duplicate workflow:

```text
POST /api/incidents/{incidentId}/media/upload
POST /api/incidents/{incidentId}/analyze
POST /api/incidents/{incidentId}/agent-workflow
GET  /api/incidents/{incidentId}/prediction
GET  /api/incidents/{incidentId}/duplicates
GET  /api/incidents/{incidentId}/similar
GET  /api/ai-evaluations/baselines
POST /api/model-lab/analyze
```

Analysis updates require `Administrator` or `Operator`. Local SDK development uses a deterministic heuristic analyzer and hashing text embeddings by default. Docker Compose enables the Python AI service through `AiService:Enabled=true` and falls back to local logic if that service is unavailable. The OpenAI analyzer is still available when `OpenAI:Enabled=true`, `OPENAI_API_KEY` is set, and the Python AI service is disabled.

Uploaded image and audio evidence is now processed before final triage. The Worker opens media through `IFileStorageService`, sends images to `/v1/images/analyze`, sends audio to `/v1/audio/transcriptions`, stores labels/transcripts/model metadata on `IncidentMedia`, and then builds the final incident prediction with those media findings included.

Prediction responses include an `evidence` array so the frontend can show why a category, severity, agency, or duplicate recommendation was produced without calling AI models directly.

Caching and object storage:

```text
Redis:Enabled=true
Redis:ConnectionString=redis:6379
FileStorage:Provider=S3
S3Storage:Endpoint=http://minio:9000
S3Storage:PublicBaseUrl=http://localhost:9000/civic-signal
```

Redis is used through `IApplicationCache`. MinIO/S3 is used through `IFileStorageService`, so the API never exposes direct database, cache, object storage, queue, or AI-service access to the frontend.

Queue processing:

```text
RabbitMq:Enabled=true
RabbitMq:HostName=rabbitmq
RabbitMq:QueueName=civicsignal.incidents.processing
RabbitMq:RetryQueueName=civicsignal.incidents.retry
RabbitMq:DeadLetterQueueName=civicsignal.incidents.dead
```

Incident creation enqueues a processing job through `IIncidentProcessingQueue`. When RabbitMQ is enabled, the Worker consumes that queue, retries failed jobs through a delayed retry queue, and dead-letters exhausted jobs. When RabbitMQ is disabled, the Worker keeps the simpler database-polling fallback for local SDK development.

Forecasting workflow:

```text
GET  /api/forecasting/incident-volume?historyDays=30&horizonDays=7
GET  /api/forecasting/incident-volume?category=RoadDamage&agencyCode=DOT
POST http://localhost:8010/v1/forecasting/incident-volume
```

The .NET API currently provides a cached moving-average/trend baseline over incident history. The AI service exposes a matching forecast contract for a future trained time-series model.

Weather context:

```text
Weather:Enabled=false
Weather:BaseUrl=https://api.weather.gov
Weather:UserAgent=CivicSignalAI/0.1 local-development
Weather:TimeoutSeconds=15
Weather:CacheMinutes=20
```

When enabled, the backend uses the National Weather Service API through `IWeatherService`. Weather data is never invented; unavailable weather is returned as an unavailable tool result without blocking every local draft work order.

Geocoding workflow:

```text
GET /api/geocoding/search?query=Pine%20St%20and%207th%20Ave
GET /api/geocoding/reverse?latitude=40.7128&longitude=-74.0060
```

```text
Nominatim:Enabled=true
Nominatim:BaseUrl=https://nominatim.openstreetmap.org
Nominatim:UserAgent=CivicSignalAI/0.1 local-development
Nominatim:SearchLimit=6
Nominatim:CountryCodes=us
```

When enabled, the backend uses OpenStreetMap Nominatim through `IGeocodingService`. The citizen PWA searches addresses and reverse-geocodes device coordinates through the .NET API, so the frontend never needs direct access to external geocoding services.

Historical NYC 311 workflow:

```text
GET  /api/historical-complaints?pageSize=300
GET  /api/historical-complaints?category=RoadDamage&agency=DOT
GET  /api/historical-complaints/summary
POST /api/data-import-jobs/nyc311
GET  /api/data-import-jobs?source=NYC311
POST /api/historical-complaints/nyc311/import
```

Search and summary are public frontend-facing APIs. Job creation and direct import require `Administrator` or `Operator`. The preferred admin workflow is `POST /api/data-import-jobs/nyc311`, which creates a tracked `data_import_jobs` record and queues it for the Worker. The Worker processes jobs through RabbitMQ when enabled, or through a local database polling fallback when RabbitMQ is disabled.

The importer reads NYC Open Data 311 records, stores them in `historical_complaints`, normalizes them into operational categories such as `RoadDamage`, `Flooding`, `Streetlight`, and `Sanitation`, and indexes their PostGIS geography point for map/radius searches.

NYC 311 configuration:

```text
Nyc311:BaseUrl=https://data.cityofnewyork.us
Nyc311:ResourcePath=/resource/erm2-nwe9.json
NYC311_APP_TOKEN=
NYC311_MAX_LIMIT=5000
```

The frontend incidents workspace now overlays imported historical 311 complaints on the operations map through the API only. It does not access PostgreSQL, NYC Open Data, object storage, queues, or AI services directly.

The frontend admin Data Sources page is available at:

```text
http://localhost:3000/admin/data-sources
```

The frontend admin AI Evaluation page is available at:

```text
http://localhost:3000/admin/ai-evaluation
```

The public and staff Model Lab pages are available at:

```text
http://localhost:3000/public/model-lab
http://localhost:3000/admin/model-lab
```

Use the seeded operator credentials, queue an NYC 311 import job, then watch `Pending`, `Running`, `Succeeded`, or `Failed` status from the job history panel.

Python AI service endpoints:

```text
GET  http://localhost:8010/health
POST http://localhost:8010/v1/incidents/analyze
POST http://localhost:8010/v1/text/embeddings
POST http://localhost:8010/v1/audio/transcriptions
POST http://localhost:8010/v1/images/analyze
POST http://localhost:8010/v1/forecasting/incident-volume
```

## Evaluation Baseline

AI quality checks live in `evaluation/`. The baseline covers requirement metrics for text classification, duplicate detection, forecasting, audio transcription, image analysis, and generated reports:

```bash
python3 evaluation/scripts/evaluate_baselines.py --write-report
python3 -m unittest discover -s evaluation/tests
```

The generated report is written to `evaluation/reports/baseline-results.md`. Future work should extend this module with system performance metrics.
