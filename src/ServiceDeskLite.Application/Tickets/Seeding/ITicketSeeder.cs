namespace ServiceDeskLite.Application.Tickets.Seeding;

public interface ITicketSeeder
{
    Task SeedAsync(CancellationToken ct = default);
}
