namespace ServiceDeskLite.Contracts.V1.Tickets;

// AssigneeName = null means "unassign"
public sealed record AssignTicketRequest(string? AssigneeName);