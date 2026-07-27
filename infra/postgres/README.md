# PostgreSQL

Initial schema source:

```text
src/CivicSignal.Infrastructure/Persistence/Migrations/
```

The backend uses EF Core migrations against PostgreSQL with PostGIS for location search and pgvector for embedding similarity.

Local Docker defaults:

```text
Host=localhost
Port=5432
Database=civic_signal
Username=postgres
Password=postgres
```

The container runs `infra/postgres/init/001_extensions.sql` on first database creation. EF migrations also declare the required extensions so fresh databases are reproducible.

Useful commands:

```bash
/Users/kritshrestha/.dotnet/dotnet tool restore
/Users/kritshrestha/.dotnet/dotnet restore CivicSignal.slnx
docker compose up -d postgres
/Users/kritshrestha/.dotnet/dotnet ef migrations add <Name> --project src/CivicSignal.Infrastructure/CivicSignal.Infrastructure.csproj --startup-project src/CivicSignal.Api/CivicSignal.Api.csproj --output-dir Persistence/Migrations
/Users/kritshrestha/.dotnet/dotnet ef database update --project src/CivicSignal.Infrastructure/CivicSignal.Infrastructure.csproj --startup-project src/CivicSignal.Api/CivicSignal.Api.csproj
```
