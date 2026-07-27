# Docker

Local infrastructure starts from the repository root:

```bash
docker compose up -d postgres
docker compose ps
docker compose logs -f postgres
```

The full local backend stack starts with:

```bash
cp .env.example .env
docker compose up --build
```

The compose stack includes PostgreSQL with PostGIS and pgvector, Redis, RabbitMQ with the management UI, MinIO/S3 object storage, the Python AI service, the ASP.NET Core API, and the background Worker. The API container can apply EF migrations on startup through `DATABASE_MIGRATE_ON_STARTUP=true` and seed demo data through `DEMO_DATA_ENABLED=true`.

The PostgreSQL image is pinned to `linux/amd64` in `docker-compose.yml` because the combined PostGIS/pgvector image does not currently publish an ARM64 manifest. Docker Desktop can run it through emulation on Apple Silicon.

Useful endpoints:

```text
http://localhost:5020/swagger
http://localhost:5020/health
http://localhost:5020/health/ready
http://localhost:15672
http://localhost:9001
```

To reset local database state:

```bash
docker compose down -v
docker compose up -d postgres
```
