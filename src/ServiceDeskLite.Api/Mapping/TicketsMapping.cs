using ServiceDeskLite.Api.Contracts.Tickets;
using ServiceDeskLite.Application.Tickets.GetTicketById;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Api.Mapping;

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

    public static TicketListItemResponse ToListItemResponse(this Ticket ticket)
        => new(
            Id: ticket.Id.Value,
            Title: ticket.Title,
            Priority: ticket.Priority.ToString(),
            Status: ticket.Status.ToString(),
            CreatedAt: ticket.CreatedAt,
            DueAt: ticket.DueAt
        );
}
