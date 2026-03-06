namespace ServiceDeskLite.Application.Tickets.GetAuditEvents;

public sealed record AuditEventDto(
    Guid Id,
    string EventType,
    string? Actor,
    DateTimeOffset OccurredAt,
    string Payload);
