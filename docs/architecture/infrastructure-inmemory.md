## Infrastructure – SQLite (`ServiceDeskLite.Infrastructure`)

#### `ServiceDeskLiteDbContext`

```csharp
public class ServiceDeskLiteDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<Ticket> Tickets => Set<Ticket>();
    // Fluent config via IEntityTypeConfiguration<T> assemblies
}
```

#### EF Core Fluent Configuration (`TicketConfiguration`)

```csharp
builder.ToTable("Tickets");
builder.HasKey(t => t.Id);
builder.Property(t => t.Id)
    .HasConversion(new TicketIdConverter()).ValueGeneratedNever();
builder.Property(t => t.Title).IsRequired().HasMaxLength(200);
builder.Property(t => t.Description).IsRequired().HasMaxLength(2000);
builder.Property(t => t.Priority).IsRequired();
builder.Property(t => t.Status).IsRequired();
builder.Property(t => t.CreatedAt)
    .HasConversion(new DateTimeOffsetToBinaryConverter()).IsRequired();
builder.Property(t => t.DueAt).IsRequired(false);
builder.HasIndex(t => t.CreatedAt);
builder.HasIndex(t => t.Status);
```

Value converter: `TicketId` ↔ `Guid` via `TicketIdConverter`.
#### Migrations

Currently, one migration: `20260219091433_InitialCreate` (Initial schema).
Migrations are auto-applied on startup when `Provider = Sqlite`.

#### EfTicketRepository
- `AddAsync` → `_dbContext.Tickets.AddAsync`
- `GetByIdAsync` → `FirstOrDefaultAsync(t => t.Id == id)`
- `ExistsAsync` → `AnyAsync(t => t.Id == id)`
- `SearchAsync` → LINQ with filters, sort, paging; tie-breaker: `.ThenBy(t => t.Id)`
-
#### `EfUnitOfWork`

```csharp
public Task SaveChangesAsync(CancellationToken ct = default)
    => _dbContext.SaveChangesAsync(ct);
```
--- 
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

#### DI Lifetime Summary

| Type                       | 	Lifetime | 	Reason                        |
|----------------------------|-----------|--------------------------------|
| `InMemoryStore`            | Singleton | 	Shared in-process state       |
| `InMemoryUnitOfWork`       | 	Scoped   | 	Per-request change set        |
| `InMemoryTicketRepository` | 	Scoped   | 	References scoped UoW         |
| `EfTicketRepository`       | 	Scoped   | 	DbContext is Scoped           |
| `EfUnitOfWork`             | 	Scoped   | 	DbContext is Scoped           |
| `Use-case handlers         | 	Scoped   | 	Reference Scoped repositories |
