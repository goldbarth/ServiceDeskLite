namespace ServiceDeskLite.Contracts.V1.Tickets;

// AgentId = null means "unassign". The agent must exist and be active (ADR-0025).
public sealed record AssignTicketRequest(Guid? AgentId);
