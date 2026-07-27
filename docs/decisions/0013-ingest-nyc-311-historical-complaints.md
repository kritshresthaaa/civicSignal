# 0013 Ingest NYC 311 Historical Complaints

## Status

Accepted

## Context

The requirements call for a historical complaint ingestion pipeline, complaint search APIs, and map-visible historical context. The frontend must not access PostgreSQL or external public data sources directly.

## Decision

Add historical complaints as a backend feature inside the existing Clean Architecture structure:

- `Domain` owns `HistoricalComplaint`.
- `Application` owns `IHistoricalComplaintService`, repository contracts, DTOs, validation, and the `INyc311ComplaintClient` port.
- `Infrastructure` owns EF Core persistence, PostGIS indexes, and the NYC Open Data/Socrata HTTP client.
- `Api` exposes public search/summary endpoints and a protected import endpoint.
- `frontend` consumes only CivicSignal API responses and overlays 311 records on the operations map.

Imported complaints are normalized into portfolio-friendly categories such as `RoadDamage`, `Flooding`, `Streetlight`, `Noise`, `Sanitation`, `Graffiti`, and `TreeHazard`. This is a deterministic starter classifier that can later be replaced or enriched by the AI service.

## Consequences

The backend now supports real historical complaint storage and map search without coupling the UI to external datasets. The import endpoint remains staff-only, and the table is indexed for source deduplication, category filters, date filters, and geospatial radius queries.
