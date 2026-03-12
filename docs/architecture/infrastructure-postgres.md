## Infrastructure – PostgreSQL / EF Core (`ServiceDeskLite.Infrastructure`)

The production persistence provider uses EF Core 10 with PostgreSQL via the Npgsql driver.
SQLite was used in Milestones 1–2 and was replaced in M3-02 (see ADR 0006).

---

### `ServiceDeskLiteDbContext`

```csharp
public class ServiceDeskLiteDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<Ticket>         Tickets        => Set<Ticket>();
    public DbSet<AuditEvent>     AuditEvents    => Set<AuditEvent>();
    public DbSet<OutboxMessage>  OutboxMessages => Set<OutboxMessage>();
    // Fluent config via IEntityTypeConfiguration<T>, auto-discovered from assembly
}
```

### EF Core Fluent Configuration

#### `TicketConfiguration`

```csharp
builder.ToTable("Tickets");
builder.HasKey(t => t.Id);
builder.Property(t => t.Id).HasConversion(new TicketIdConverter()).ValueGeneratedNever();
builder.Property(t => t.Title).IsRequired().HasMaxLength(200);
builder.Property(t => t.Description).IsRequired().HasMaxLength(2000);
builder.Property(t => t.Priority).IsRequired();
builder.Property(t => t.Status).IsRequired();
builder.Property(t => t.CreatedAt).IsRequired();          // timestamptz — no converter needed
builder.Property(t => t.DueAt).IsRequired(false);
builder.Property(t => t.Assignee).HasMaxLength(100).IsRequired(false);
builder.HasMany(t => t.Comments).WithOne().HasForeignKey("TicketId").OnDelete(DeleteBehavior.Cascade);
builder.HasIndex(t => t.CreatedAt);
builder.HasIndex(t => t.Status);
```

> Unlike the earlier SQLite provider, PostgreSQL natively handles `timestamptz` — no
> `DateTimeOffset → long` converters are needed (see ADR 0006).

#### `CommentConfiguration`

```csharp
builder.ToTable("TicketComments");
builder.HasKey(c => c.Id);
builder.Property(c => c.Id).HasConversion(new CommentIdConverter()).ValueGeneratedNever();
builder.Property(c => c.Content).IsRequired().HasMaxLength(2000);
builder.Property(c => c.Author).HasMaxLength(100).IsRequired(false);
builder.Property(c => c.CreatedAt).IsRequired();
```

#### `AuditEventConfiguration`

```csharp
builder.ToTable("AuditEvents");
builder.HasKey(e => e.Id);
builder.Property(e => e.Id).HasConversion(new AuditEventIdConverter()).ValueGeneratedNever();
builder.Property(e => e.TicketId).HasConversion(new TicketIdConverter()).IsRequired();
builder.Property(e => e.EventType).HasMaxLength(100).IsRequired();
builder.Property(e => e.Actor).HasMaxLength(200).IsRequired(false);
builder.Property(e => e.OccurredAt).IsRequired();
builder.Property(e => e.Payload).IsRequired();           // JSON string, no FK to Tickets
builder.HasIndex(e => e.TicketId);
builder.HasIndex(e => e.OccurredAt);
```

No FK constraint to `Tickets` — audit records survive ticket deletion.

#### `OutboxMessageConfiguration`

```csharp
builder.ToTable("OutboxMessages");
builder.HasKey(m => m.Id);
builder.Property(m => m.Id).HasConversion(new OutboxMessageIdConverter()).ValueGeneratedNever();
builder.Property(m => m.EventType).HasMaxLength(100).IsRequired();
builder.Property(m => m.Payload).IsRequired();
builder.Property(m => m.OccurredAt).IsRequired();
builder.Property(m => m.DispatchedAt).IsRequired(false);
builder.Ignore(m => m.Status);                           // Computed from DispatchedAt
builder.HasIndex(m => m.OccurredAt);
builder.HasIndex(m => m.DispatchedAt);
```

### Migrations

Migrations are auto-applied on startup when `Provider = Postgres`.

| Migration | Content |
|-----------|---------|
| `InitialCreate` | `Tickets`, `TicketComments`, `AuditEvents` tables |
| `AddOutboxMessages` | `OutboxMessages` table |

### Repositories

| Class | Interface | Notes |
|---|---|---|
| `EfTicketRepository` | `ITicketRepository` | `AddAsync`, `GetByIdAsync`, `ExistsAsync`, `SearchAsync` with LINQ |
| `EfAuditEventRepository` | `IAuditEventRepository` | `AddAsync`, `GetByTicketIdAsync` |
| `EfOutboxRepository` | `IOutboxRepository` | `AddAsync` (write-side only — showcase stub) |
| `EfDashboardRepository` | `IDashboardRepository` | `GetSummaryAsync` — aggregates KPI counts |
| `EfUnitOfWork` | `IUnitOfWork` | Delegates to `_dbContext.SaveChangesAsync` |

### Component Relationships

<img src="../assets/diagrams/component-relationships-sqlite.svg" alt="Component Relationships" style="max-width:720px;width:100%;">

### DI Lifetime

| Type | Lifetime | Reason |
|---|---|---|
| `ServiceDeskLiteDbContext` | Scoped | EF Core standard |
| `EfTicketRepository` | Scoped | References scoped DbContext |
| `EfAuditEventRepository` | Scoped | References scoped DbContext |
| `EfOutboxRepository` | Scoped | References scoped DbContext |
| `EfDashboardRepository` | Scoped | References scoped DbContext |
| `EfUnitOfWork` | Scoped | References scoped DbContext |

### Docker Compose

```yaml
services:
  db:
    image: postgres:17
    environment:
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres
      POSTGRES_DB: servicedesklite
    ports:
      - "5432:5432"
```

Run with `docker compose up` before starting the API with `Persistence:Provider = Postgres`.

### Related

- ADR 0006 – EF Core + SQLite (M1–M2) → superseded by PostgreSQL in M3
- ADR 0007 – Swappable Persistence via Runtime Provider Switch
- ADR 0008 – Unit-of-Work Pattern
- ADR 0021 – Outbox Pattern Stub