using System.Text.Json;

using ServiceDeskLite.Application.Tickets.GetAuditEvents;
using ServiceDeskLite.Contracts.V1.Tickets;
using ServiceDeskLite.Domain.Audit;

namespace ServiceDeskLite.Api.Mapping.Tickets;

internal static class AuditEventMapping
{
    // The factory serializes payloads with camelCase property names (anonymous types).
    // Our payload records use PascalCase, so case-insensitive matching is required.
    private static readonly JsonSerializerOptions PayloadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static AuditEventResponse ToResponse(this AuditEventDto dto)
        => new(dto.Id, dto.EventType, dto.Actor, dto.OccurredAt, MapPayload(dto));

    private static AuditEventPayload MapPayload(AuditEventDto dto)
        => dto.EventType switch
        {
            AuditEventTypes.TicketCreated =>
                JsonSerializer.Deserialize<TicketCreatedPayload>(dto.Payload, PayloadOptions)
                ?? new TicketCreatedPayload(string.Empty, string.Empty),

            AuditEventTypes.StatusChanged =>
                JsonSerializer.Deserialize<TicketStatusChangedPayload>(dto.Payload, PayloadOptions)
                ?? new TicketStatusChangedPayload(string.Empty, string.Empty),

            AuditEventTypes.AssigneeChanged =>
                JsonSerializer.Deserialize<TicketAssigneeChangedPayload>(dto.Payload, PayloadOptions)
                ?? new TicketAssigneeChangedPayload(null, null),

            AuditEventTypes.CommentAdded =>
                JsonSerializer.Deserialize<TicketCommentAddedPayload>(dto.Payload, PayloadOptions)
                ?? new TicketCommentAddedPayload(string.Empty, string.Empty),

            AuditEventTypes.DetailsUpdated =>
                JsonSerializer.Deserialize<TicketDetailsUpdatedPayload>(dto.Payload, PayloadOptions)
                ?? new TicketDetailsUpdatedPayload(null, null, null, null),

            _ => new RawAuditEventPayload(dto.Payload)
        };
}