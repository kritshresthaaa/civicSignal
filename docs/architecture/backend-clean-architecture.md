# Backend Clean Architecture

The backend starts as a modular monolith with five projects:

- `CivicSignal.Api`: ASP.NET Core controllers and HTTP contracts.
- `CivicSignal.Application`: application services, DTOs, validators, and interfaces.
- `CivicSignal.Domain`: business entities, value objects, and rules.
- `CivicSignal.Infrastructure`: database, object storage, AI, queue, and external API adapters.
- `CivicSignal.Worker`: background processing host for async jobs.

The first bounded context is `Incidents`. Later contexts can include `Reviews`, `Triage`, `Datasets`, `Forecasting`, and `Operations`.

Persistence starts in `CivicSignal.Infrastructure` with EF Core, PostgreSQL, PostGIS, and pgvector. Application code depends on persistence abstractions, not EF Core provider details.

Application workflows should be exposed through focused services such as `IIncidentService` so the backend stays easy to navigate while the domain is still small.

Repositories follow a hybrid pattern: use `IGenericRepository<TEntity>` for shared CRUD operations and specialized repositories such as `IIncidentRepository` for domain-specific queries.

Access management uses ASP.NET Core Identity in the Infrastructure layer. Role and policy names are defined in Application so API authorization attributes stay consistent without depending on Infrastructure types.

The API exposes `/api/auth/register`, `/api/auth/login`, `/api/auth/refresh`, `/api/auth/logout`, and `/api/auth/me`. Login issues a JWT access token plus an opaque rotating refresh token. Browsers receive them as HttpOnly cookies; API clients can send the access token with `Authorization: Bearer`. Public users register as `Reporter`; staff workflows require `IncidentReview` or `IncidentOperations`.

Incident review is a protected staff workflow. `POST /api/incidents/{incidentId}/review` requires the `IncidentReview` policy and stores the latest decision, note, reviewer user id, corrected category, corrected agency, corrected severity, duplicate marker, AI-accepted flag, and review timestamp on the incident.

Every review also creates an immutable `IncidentReviewRecord`. `GET /api/incidents/{incidentId}/reviews` exposes the audit history to staff clients.

Incident processing status is tracked as child steps under each incident. `GET /api/incidents/{incidentId}/status` exposes the current incident status plus all processing steps. `POST /api/incidents/{incidentId}/processing-status` requires the `IncidentOperations` policy and updates steps such as geocoding, media analysis, duplicate checks, and triage.

Incident text is embedded at creation time through an `ITextEmbeddingGenerator` abstraction. Infrastructure stores that vector in PostgreSQL `pgvector` and uses an HNSW cosine index for candidate ranking.

Duplicate detection combines pgvector cosine similarity, PostGIS radius filtering, and time proximity. Results are stored as `DuplicateCandidate` children and exposed through `/api/incidents/{incidentId}/duplicates` and `/api/incidents/{incidentId}/similar`.

Triage predictions are evidence-backed. `TriagePrediction` stores model name, model version, prompt version, processing time, and child `PredictionEvidence` records. Evidence records give the frontend reviewer-visible reasons without coupling clients to the AI provider.

The API exposes `/health` for liveness and `/health/ready` for PostgreSQL readiness.
