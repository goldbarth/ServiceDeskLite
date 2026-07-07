using ServiceDeskLite.Domain.Agents;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Application.Tickets.AssignTicket;

// AgentId = null means "unassign". The agent must exist and be active (checked in the handler).
public sealed record AssignTicketCommand(TicketId Id, AgentId? AgentId, string? Actor = null);
