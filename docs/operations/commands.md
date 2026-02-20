## Common Commands

#### Build and Test

```
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

#### Run Applications

```csharp
# API (defaults to InMemory in Development)
dotnet run --project src/ServiceDeskLite.Api

# Web (Blazor frontend)
dotnet run --project src/ServiceDeskLite.Web
```

#### EF Core Migrations

```
# Add a new migration (from repo root)
dotnet ef migrations add <MigrationName> \
  --project src/ServiceDeskLite.Infrastructure \
  --startup-project src/ServiceDeskLite.Api

# Apply pending migrations
dotnet ef database update \
  --project src/ServiceDeskLite.Infrastructure \
  --startup-project src/ServiceDeskLite.Api
```
