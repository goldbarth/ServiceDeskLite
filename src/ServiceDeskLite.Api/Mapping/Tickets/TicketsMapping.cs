using ServiceDeskLite.Application.Tickets.GetTicketById;
using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Contracts.V1.Tickets;

namespace ServiceDeskLite.Api.Mapping.Tickets;

public static class TicketsMapping
{
    public static TicketResponse ToResponse(this TicketDetailsDto dto)
        => new(
            Id: dto.Id.Value,
            Title: dto.Title,
            Description: dto.Description,
            Priority: dto.Priority.ToString(),
            Status: dto.Status.ToString(),
            CreatedAt: dto.CreatedAt,
            DueAt: dto.DueAt
        );

    public static TicketListItemResponse ToListItemResponse(this TicketListItemDto dto)
        => new(
            Id: dto.Id.Value,
            Title: dto.Title,
            Priority: dto.Priority.ToString(),
            Status: dto.Status.ToString(),
            CreatedAt: dto.CreatedAt,
            DueAt: dto.DueAt
        );
}
