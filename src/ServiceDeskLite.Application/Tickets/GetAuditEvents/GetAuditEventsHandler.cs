using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Common;

namespace ServiceDeskLite.Application.Tickets.GetAuditEvents;

public sealed class GetAuditEventsHandler
{
    private readonly ITicketRepository _ticketRepository;
    private readonly IAuditEventRepository _auditRepository;

    public GetAuditEventsHandler(
        ITicketRepository ticketRepository,
        IAuditEventRepository auditRepository)
    {
        _ticketRepository = ticketRepository ?? throw new ArgumentNullException(nameof(ticketRepository));
        _auditRepository = auditRepository ?? throw new ArgumentNullException(nameof(auditRepository));
    }

    public async Task<Result<IReadOnlyList<AuditEventDto>>> HandleAsync(
        GetAuditEventsQuery? query,
        CancellationToken ct = default)
    {
        if (query is null)
            return Result<IReadOnlyList<AuditEventDto>>.Validation(
                "get_audit_events.query.null",
                "Query must not be null.");

        var exists = await _ticketRepository.ExistsAsync(query.TicketId, ct);
        if (!exists)
            return Result<IReadOnlyList<AuditEventDto>>.NotFound(
                "ticket.not_found",
                "Ticket was not found.",
                new Dictionary<string, object> { ["ticketId"] = query.TicketId }!);

        var events = await _auditRepository.GetByTicketIdAsync(query.TicketId, ct);

        var dtos = events
            .Select(e => new AuditEventDto(e.Id.Value, e.EventType, e.Actor, e.OccurredAt, e.Payload))
            .ToList();

        return Result<IReadOnlyList<AuditEventDto>>.Success(dtos);
    }
}
