using System;
using System.Threading;
using System.Threading.Tasks;

using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Tickets;
using ServiceDeskLite.Application.Tickets.Seeding;
using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Infrastructure.Persistence.Seeding;

public sealed class TicketSeeder : ITicketSeeder
{
    private readonly ITicketRepository _tickets;
    private readonly IUnitOfWork _uow;

    public TicketSeeder(ITicketRepository tickets, IUnitOfWork uow)
    {
        _tickets = tickets;
        _uow = uow;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        // Check if at least one ticket exists.
        var existing = await _tickets.SearchAsync(
            criteria: TicketSearchCriteria.Empty,
            paging: new Paging(Page: 1, PageSize: 1),
            sort: SortSpec.Default,
            ct: ct);

        if (existing.TotalCount > 0)
            return;

        var now = DateTimeOffset.UtcNow;

        var t1 = new Ticket(
            id: TicketId.New(),
            title: "Cannot login",
            description: "User cannot login to the portal.",
            priority: TicketPriority.High,
            createdAt: now.AddMinutes(-50),
            dueAt: now.AddDays(2));

        var t2 = new Ticket(
            id: TicketId.New(),
            title: "Printer issue",
            description: "Office printer shows error code E13.",
            priority: TicketPriority.Medium,
            createdAt: now.AddMinutes(-40));

        var t3 = new Ticket(
            id: TicketId.New(),
            title: "VPN unstable",
            description: "VPN disconnects every 10 minutes.",
            priority: TicketPriority.Medium,
            createdAt: now.AddMinutes(-30),
            dueAt: now.AddDays(1));

        var t4 = new Ticket(
            id: TicketId.New(),
            title: "Request access",
            description: "Need access to finance folder.",
            priority: TicketPriority.Low,
            createdAt: now.AddMinutes(-20));

        var t5 = new Ticket(
            id: TicketId.New(),
            title: "Update software",
            description: "Please update the ticketing client.",
            priority: TicketPriority.Low,
            createdAt: now.AddMinutes(-10));

        await _tickets.AddAsync(t1, ct);
        await _tickets.AddAsync(t2, ct);
        await _tickets.AddAsync(t3, ct);
        await _tickets.AddAsync(t4, ct);
        await _tickets.AddAsync(t5, ct);

        await _uow.SaveChangesAsync(ct);
    }
}
