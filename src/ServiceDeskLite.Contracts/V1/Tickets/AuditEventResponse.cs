namespace ServiceDeskLite.Contracts.V1.Tickets;

public sealed record AuditEventResponse(
    Guid Id,
    string EventType,
    string? Actor,
    DateTimeOffset OccurredAt,
    string Payload);
