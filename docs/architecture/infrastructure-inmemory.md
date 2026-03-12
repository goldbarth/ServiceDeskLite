## Infrastructure – InMemory (`ServiceDeskLite.Infrastructure.InMemory`)

The InMemory provider is used in Development and for all automated tests.
It mirrors the PostgreSQL provider's port interfaces exactly.

---

### `InMemoryStore` – Singleton

```csharp
internal sealed class InMemoryStore
{
    private readonly ConcurrentDictionary<TicketId, Ticket> _tickets = new();
    private readonly ConcurrentBag<AuditEvent>              _auditEvents = new();
    private readonly ConcurrentBag<OutboxMessage>           _outboxMessages = new();

    // Tickets
    public bool TryGetTicket(TicketId id, out Ticket? ticket)
    public bool ContainsTicket(TicketId id)
    public IReadOnlyCollection<Ticket> SnapshotTickets()
    public void ApplyAdds(IEnumerable<Ticket> adds)          // Throws on duplicate

    // Audit events
    public void AppendAuditEvents(IEnumerable<AuditEvent> events)
    public IReadOnlyList<AuditEvent> GetAuditEventsByTicketId(TicketId ticketId)
    public IReadOnlyCollection<AuditEvent> SnapshotAuditEvents()

    // Outbox
    public void AppendOutboxMessages(IEnumerable<OutboxMessage> messages)
    public IReadOnlyCollection<OutboxMessage> SnapshotOutboxMessages()
}
```

Data persists for the application lifetime. Shared across all scoped requests.

### `InMemoryUnitOfWork` – Scoped

```csharp
internal sealed class InMemoryUnitOfWork : IUnitOfWork
{
    internal List<object> PendingAdds { get; } = [];

    public Task SaveChangesAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var ticketAdds = PendingAdds.OfType<Ticket>().ToArray();
        if (ticketAdds.Length > 0) _store.ApplyAdds(ticketAdds);

        var auditEventAdds = PendingAdds.OfType<AuditEvent>().ToArray();
        if (auditEventAdds.Length > 0) _store.AppendAuditEvents(auditEventAdds);

        var outboxAdds = PendingAdds.OfType<OutboxMessage>().ToArray();
        if (outboxAdds.Length > 0) _store.AppendOutboxMessages(outboxAdds);

        PendingAdds.Clear();
        return Task.CompletedTask;
    }
}
```

All three entity types (Ticket, AuditEvent, OutboxMessage) are flushed atomically.

### Repositories

| Class | Interface | Notes |
|---|---|---|
| `InMemoryTicketRepository` | `ITicketRepository` | `AddAsync` → PendingAdds; reads → snapshot |
| `InMemoryAuditEventRepository` | `IAuditEventRepository` | `AddAsync` → PendingAdds; `GetByTicketIdAsync` → store |
| `InMemoryOutboxRepository` | `IOutboxRepository` | `AddAsync` → PendingAdds |
| `InMemoryDashboardRepository` | `IDashboardRepository` | `GetSummaryAsync` → computed from snapshot |

### Component Relationships

<img src="../assets/diagrams/component-relationships-inmemory.svg" alt="Component Relationships" style="max-width:360px;width:100%;">

### Unit of Work Commit Boundary

The two-phase write (buffer → commit) prevents partially visible state within a request scope.

<img src="../assets/diagrams/uow-commit-boundary.svg" alt="Unit of Work Commit Boundary" style="max-width:720px;width:100%;">

### DI Lifetime Summary

| Type                           | Lifetime  | Reason                         |
|--------------------------------|-----------|--------------------------------|
| `InMemoryStore`                | Singleton | Shared in-process state        |
| `InMemoryUnitOfWork`           | Scoped    | Per-request change set         |
| `InMemoryTicketRepository`     | Scoped    | References scoped UoW          |
| `InMemoryAuditEventRepository` | Scoped    | References scoped UoW          |
| `InMemoryOutboxRepository`     | Scoped    | References scoped UoW          |
| `InMemoryDashboardRepository`  | Scoped    | References singleton store     |
| Use-case handlers              | Scoped    | Reference scoped repositories  |
