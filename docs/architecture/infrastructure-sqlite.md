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
