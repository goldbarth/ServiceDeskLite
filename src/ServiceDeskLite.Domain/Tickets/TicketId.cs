namespace ServiceDeskLite.Domain.Tickets;

public readonly record struct TicketId(Guid Value)
{
    public static TicketId New() => new(Guid.NewGuid());
}
