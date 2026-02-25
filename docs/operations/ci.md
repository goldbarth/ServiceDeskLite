## CI/CD (GitHub Actions)

### `ci.yml` – Build & Test

Triggers: push and pull request to `main`.

1. Checkout
2. Setup .NET 10
3. `dotnet restore` (with `packages.lock.json` cache)
4. `dotnet build -c Release --no-restore`
5. `dotnet test -c Release --no-build`
   Matrix: ubuntu-latest and windows-latest.

Lock files (`packages.lock.json`) are committed per project and must be kept up to date.
Run `dotnet restore` after adding/updating any NuGet package.

---

### `docs.yml` – Documentation Site

Triggers: push to `main`, manual (`workflow_dispatch`).
Deploys to GitHub Pages via two sequential jobs:

**`build` job:**

1. Checkout
2. Setup .NET 10
3. Setup Node.js 20 (cache: npm, path: `.build/docs/package-lock.json`)
4. Install docs tooling – `npm ci` in `.build/docs`
5. Generate diagrams – `npm run generate` (Mermaid → SVG)
6. Install DocFX globally – `dotnet tool update -g docfx`
7. Generate API docs – `dotnet run --project ./tools/ServiceDeskLite.DocsGen -c Release`
8. Prepare Swagger UI assets – `npm run swagger-ui` in `.build/docs`
9. Build site – `docfx ./docs/docfx.json` → output: `docs/_site`
10. Upload Pages artifact (`actions/upload-pages-artifact@v3`, path: `docs/_site`)

**`deploy` job** (needs `build`):

1. Deploy to GitHub Pages (`actions/deploy-pages@v4`)

Required permissions: `contents: read`, `pages: write`, `id-token: write`.

---

### `openapi-snapshot.yml` – OpenAPI Snapshot

Triggers: manual only (`workflow_dispatch`).

1. Checkout
2. Setup .NET 10
3. `dotnet restore ./ServiceDeskLite.slnx`
4. `dotnet build ./ServiceDeskLite.slnx -c Release --no-restore`
5. Generate snapshot – `.build/docs/generate-openapi.sh` → `docs/api/openapi.v1.json`
6. Upload artifact `openapi-v1-snapshot` (`actions/upload-artifact@v4`, fails if file missing)

Use this workflow to capture a fresh OpenAPI spec without triggering a full docs build.

---

### Key Files for Orientation

| File                                                                          | 	Purpose                                      |
|-------------------------------------------------------------------------------|-----------------------------------------------|
| `src/ServiceDeskLite.Api/Program.cs`                                          | 	API composition root and middleware pipeline |
| `src/ServiceDeskLite.Web/Program.cs`                                          | 	Blazor composition root                      |
| `src/ServiceDeskLite.Api/Composition/InfrastructureComposition.cs`            | 	Provider switch (InMemory vs Sqlite)         |
| `src/ServiceDeskLite.Api/Endpoints/TicketsEndpoints.cs`	All API endpoint      | definitions                                   |
| `src/ServiceDeskLite.Api/Mapping/Tickets/TicketEnumMapping.cs`                | 	Contracts ↔ Domain/App mapping               |
| `src/ServiceDeskLite.Application/Common/Result.cs`                            | 	Result monad (void)                          |
| `src/ServiceDeskLite.Application/Common/ApplicationError.cs`                  | 	Error record + ErrorType enum                |
| `src/ServiceDeskLite.Application/Common/PagingPolicy.cs`                      | 	Paging constants                             |
| `src/ServiceDeskLite.Domain/Tickets/Ticket.cs`                                | 	Aggregate root                               |
| `src/ServiceDeskLite.Domain/Tickets/TicketWorkflow.cs`                        | 	Status transition rules                      |
| `src/ServiceDeskLite.Domain/Common/Guard.cs`                                  | 	Invariant enforcement                        |
| `src/ServiceDeskLite.Api/Http/ProblemDetails/ResultToProblemDetailsMapper.cs` | 	Result → HTTP mapping                        |
| `src/ServiceDeskLite.Infrastructure/Persistence/ServiceDeskLiteDbContext.cs`  | 	EF Core DbContext                            |
| `src/ServiceDeskLite.Infrastructure.InMemory/Persistence/InMemoryStore.cs`    | 	In-memory data store                         |
| `src/ServiceDeskLite.Web/Api/V1/TicketsApiClient.cs`                          | 	HTTP client for API                          |
| `tests/ServiceDeskLite.Tests.EndToEnd/Composition/TestServiceProvider.cs`     | 	E2E DI bootstrap                             |
| `tests/ServiceDeskLite.Tests.Api/Infrastructure/ApiWebApplicationFactory.cs`  | 	API test factory                             |
