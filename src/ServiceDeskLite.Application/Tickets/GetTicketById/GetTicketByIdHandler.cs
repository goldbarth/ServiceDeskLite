using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Application.Tickets.GetAuditEvents;
using ServiceDeskLite.Application.Tickets.Shared;

namespace ServiceDeskLite.Application.Tickets.GetTicketById;

public sealed class GetTicketByIdHandler
{
    private readonly ITicketRepository _repository;
    private readonly IAgentRepository _agentRepository;
    private readonly IAuditEventRepository _auditRepository;
    private readonly IClock _clock;

    public GetTicketByIdHandler(
        ITicketRepository repository,
        IAgentRepository agentRepository,
        IAuditEventRepository auditRepository,
        IClock clock)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _auditRepository = auditRepository ?? throw new ArgumentNullException(nameof(auditRepository));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<TicketDetailsDto>> HandleAsync(
        GetTicketByIdQuery? query,
        CancellationToken ct = default)
    {
        if (query is null)
            return Result<TicketDetailsDto>.Validation(
                "get_ticket_by_id.query.null",
                "Query must not be null.");

        var ticket = await _repository.GetByIdAsync(query.Id, ct);

        if (ticket is null)
        {
            return Result<TicketDetailsDto>.NotFound(
                "ticket.not_found",
                "Ticket was not found.",
                meta: new Dictionary<string, object?>{["ticketId"] = query.Id}!);
        }

        var auditEvents = await _auditRepository.GetByTicketIdAsync(query.Id, ct);
        var auditEventDtos = auditEvents
            .Select(e => new AuditEventDto(e.Id.Value, e.EventType, e.Actor, e.OccurredAt, e.Payload))
            .ToList();

        var assigneeName = ticket.AssignedAgentId is { } agentId
            ? (await _agentRepository.GetByIdAsync(agentId, ct))?.Name
            : null;

        var dto = ticket.ToDetailsDto(auditEventDtos, _clock.UtcNow, assigneeName);

        return Result<TicketDetailsDto>.Success(dto);
    }
}
