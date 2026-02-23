namespace ServiceDeskLite.Contracts.V1.Tickets;

public sealed record ChangeTicketStatusRequest(TicketStatus NewStatus);
