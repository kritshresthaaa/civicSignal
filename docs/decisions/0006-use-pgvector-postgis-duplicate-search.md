# 0006 Use pgvector and PostGIS for Duplicate Search

## Status

Accepted

## Context

The requirements call for duplicate detection using text similarity, geographic distance, and time proximity. The earlier implementation compared recent incidents in memory, which was useful for a scaffold but did not exercise the intended PostgreSQL/PostGIS/pgvector backend.

## Decision

Generate a deterministic 1024-d text embedding when an incident is created and store it in `incidents.text_embedding`. Add an HNSW cosine index over that vector column.

Use Infrastructure's duplicate-search adapter to query PostgreSQL with:

- pgvector cosine distance for text similarity
- PostGIS `ST_DWithin` and `ST_Distance` for radius filtering
- incident creation timestamps for time proximity

Expose stored candidates through both `/duplicates` and `/similar`.

## Consequences

The backend now demonstrates real vector and geospatial database usage without requiring a Python AI service during local development. The hashing embedding generator is a deterministic local baseline; a Hugging Face or OpenAI embedding generator can replace it later behind the same `ITextEmbeddingGenerator` interface.
