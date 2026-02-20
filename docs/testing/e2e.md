## End-to-End Tests (`[ProviderMatrix]`)

```csharp
[Theory, ProviderMatrix]
public async Task CreateThenFind_ReturnsTicket(PersistenceProvider provider)
{
    await using var services = TestServiceProvider.Create(provider);
    // ...
}
```

TestServiceProvider.Create(provider) boots a full DI container with either
InMemory or Sqlite (in-memory SQLite DB). Key E2E scenarios:

- `CommitBoundaryTests` – Unit of Work flush behavior
- `DuplicateDetectionTests` – Conflict result path
- `DeterministicPagingSortingTests` – `Stable CreatedAt + Id` ordering
- `ReadIsolationTests` – No uncommitted-read leakage between scopes API Tests

`ApiWebApplicationFactory` inherits `WebApplicationFactory<Program>` and overrides
persistence with InMemory.
