using ServiceDeskLite.Application.Tickets.CreateTicket;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Tests.EndToEnd.Composition;

internal static class TicketFactory
{
    private static int _counter;

    public static CreateTicketCommand Command(
        DateTimeOffset? createdAt = null,
        TicketPriority priority = TicketPriority.Medium,
        string? title = null)
    {
        var idx = Interlocked.Increment(ref _counter);
        return new CreateTicketCommand(
            Title: title ?? $"Ticket #{idx}",
            Description: $"Description for ticket #{idx}",
            Priority: priority,
            CreatedAt: createdAt ?? DateTimeOffset.UtcNow);
    }
}
