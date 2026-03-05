using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Application.Tickets.GetAuditEvents;

public sealed record GetAuditEventsQuery(TicketId TicketId);
