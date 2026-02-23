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

    private static readonly string[] Titles =
    [
        "Cannot login", "Printer issue", "VPN unstable", "Request access", "Update software",
        "Email not syncing", "Monitor flickering", "Keyboard unresponsive", "Cannot open Excel",
        "Outlook crash on startup", "Slow network in meeting room", "No sound after update",
        "Password reset required", "Missing drive mapping", "Scanner offline",
        "Browser plugin broken", "Teams call drops", "File share permission denied",
        "CPU fan noise", "Battery drains fast", "Projector not detected", "USB port dead",
        "Antivirus blocking app", "Clock out of sync", "Remote desktop timeout",
    ];

    private static readonly string[] Descriptions =
    [
        "Affects multiple users in the same department.",
        "Reproducible on every attempt.",
        "Started after last Windows update.",
        "Only occurs on certain machines.",
        "User urgently needs this resolved.",
        "Temporary workaround in place.",
        "No error message shown.",
        "Error code visible in event log.",
        "Happens intermittently.",
        "Confirmed on two separate devices.",
    ];

    private static readonly TicketPriority[] Priorities =
        [TicketPriority.Low, TicketPriority.Medium, TicketPriority.High, TicketPriority.Critical];

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
        const int count = 100;

        for (var i = 0; i < count; i++)
        {
            var title = $"{Titles[i % Titles.Length]} #{i + 1}";
            var description = Descriptions[i % Descriptions.Length];
            var priority = Priorities[i % Priorities.Length];
            var createdAt = now.AddMinutes(-(count - i) * 15);
            var dueAt = i % 3 == 0 ? createdAt.AddDays(3 + i % 7) : (DateTimeOffset?)null;

            var ticket = new Ticket(
                id: TicketId.New(),
                title: title,
                description: description,
                priority: priority,
                createdAt: createdAt,
                dueAt: dueAt);

            await _tickets.AddAsync(ticket, ct);
        }

        await _uow.SaveChangesAsync(ct);
    }
}
