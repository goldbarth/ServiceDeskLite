## Infrastructure – InMemory (`ServiceDeskLite.Infrastructure.InMemory`)

#### `InMemoryStore` – Singleton

```csharp
internal sealed class InMemoryStore
{
    private readonly ConcurrentDictionary<TicketId, Ticket> _tickets = new();

    public bool TryGetTicket(TicketId id, out Ticket? ticket)
    public bool ContainsTicket(TicketId id)
    public IReadOnlyCollection<Ticket> SnapshotTickets()
    public void ApplyAdds(IEnumerable<Ticket> adds)   // Throws on duplicate
}
```
Data persists for the application lifetime. Shared across all scoped requests.

#### `InMemoryUnitOfWork` – Scoped

```csharp
internal sealed class InMemoryUnitOfWork : IUnitOfWork
{
    internal List<object> PendingAdds { get; } = [];

    public Task SaveChangesAsync(CancellationToken ct = default)
    {
        var ticketAdds = PendingAdds.OfType<Ticket>().ToArray();
        if (ticketAdds.Length > 0) _store.ApplyAdds(ticketAdds);
        PendingAdds.Clear();
    }
}
```

#### InMemoryTicketRepository – Scoped

- `AddAsync` → appends to `unitOfWork.PendingAdds`
- `GetByIdAsync` / `ExistsAsync` → synchronous lookup on `InMemoryStore`
- `SearchAsync` → LINQ in-memory with same filter/sort logic; tie-breaker: `.ThenBy(t => t.Id.Value)`

#### Component Relationships

```mermaid
classDiagram
    class InMemoryStore {
        <<Singleton>>
        -ConcurrentDictionary~TicketId,Ticket~ _tickets
        +TryGetTicket(id, out ticket) bool
        +ContainsTicket(id) bool
        +SnapshotTickets() IReadOnlyCollection~Ticket~
        +ApplyAdds(adds) void
    }
    class InMemoryUnitOfWork {
        <<Scoped>>
        -InMemoryStore _store
        +List~object~ PendingAdds
        +SaveChangesAsync(ct) Task
    }
    class InMemoryTicketRepository {
        <<Scoped>>
        -InMemoryStore _store
        -InMemoryUnitOfWork _unitOfWork
        +AddAsync(ticket, ct)
        +GetByIdAsync(id, ct)
        +ExistsAsync(id, ct)
        +SearchAsync(criteria, paging, sort, ct)
    }
    class ITicketRepository {
        <<interface>>
    }
    class IUnitOfWork {
        <<interface>>
    }

    InMemoryTicketRepository ..|> ITicketRepository : implements
    InMemoryUnitOfWork ..|> IUnitOfWork : implements
    InMemoryTicketRepository --> InMemoryStore : reads from
    InMemoryTicketRepository --> InMemoryUnitOfWork : buffers adds in
    InMemoryUnitOfWork --> InMemoryStore : commits to
```

#### Unit of Work Commit Boundary

The two-phase write (buffer → commit) prevents partially visible state within a request scope.

```mermaid
sequenceDiagram
    participant H as Handler
    participant Repo as InMemoryTicketRepository
    participant UoW as InMemoryUnitOfWork (Scoped)
    participant Store as InMemoryStore (Singleton)

    H->>Repo: AddAsync(ticket)
    Repo->>UoW: PendingAdds.Add(ticket)
    Note over UoW: ticket buffered – not yet visible

    H->>UoW: SaveChangesAsync()
    UoW->>Store: ApplyAdds([ticket])
    Note over Store: ConcurrentDictionary.TryAdd\nthrows on duplicate key
    Store-->>UoW: ok
    UoW->>UoW: PendingAdds.Clear()
    UoW-->>H: completed
```

#### DI Lifetime Summary

| Type                       | Lifetime  | Reason                         |
|----------------------------|-----------|--------------------------------|
| `InMemoryStore`            | Singleton | Shared in-process state        |
| `InMemoryUnitOfWork`       | Scoped    | Per-request change set         |
| `InMemoryTicketRepository` | Scoped    | References scoped UoW          |
| `EfTicketRepository`       | Scoped    | DbContext is Scoped            |
| `EfUnitOfWork`             | Scoped    | DbContext is Scoped            |
| Use-case handlers          | Scoped    | Reference Scoped repositories  |