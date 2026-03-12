## Common Commands

### Build and Test

```bash
# Restore (uses lock files – required before first build)
dotnet restore ./ServiceDeskLite.slnx

# Build
dotnet build ./ServiceDeskLite.slnx -c Release

# Run all tests
dotnet test ./ServiceDeskLite.slnx -c Release

# Run a single test project
dotnet test tests/ServiceDeskLite.Tests.Domain/

# Run specific test by name filter
dotnet test --filter "FullyQualifiedName~TicketTests"
```

### Run Applications

```bash
# API (defaults to InMemory in Development)
dotnet run --project src/ServiceDeskLite.Api

# Web (Blazor frontend)
dotnet run --project src/ServiceDeskLite.Web
```

### Docker Compose (PostgreSQL)

```bash
# Start PostgreSQL in the background
docker compose up -d

# Start all services (API + DB)
docker compose up --build

# Stop and remove containers
docker compose down

# Stop and remove containers + volumes (wipes database)
docker compose down -v
```

When running with Docker Compose, set the API persistence provider:

```json lines
// appsettings.json or environment variable
{ "Persistence": { "Provider": "Postgres" } }
```

### EF Core Migrations

```bash
# Add a new migration (from repo root)
dotnet ef migrations add <MigrationName> \
  --project src/ServiceDeskLite.Infrastructure \
  --startup-project src/ServiceDeskLite.Api

# Apply pending migrations
dotnet ef database update \
  --project src/ServiceDeskLite.Infrastructure \
  --startup-project src/ServiceDeskLite.Api
```

### Lock Files

After adding or updating any NuGet package, regenerate lock files:

```bash
dotnet restore --force-evaluate ./ServiceDeskLite.slnx
```