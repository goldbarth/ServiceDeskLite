using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Application.Tickets.Shared;

namespace ServiceDeskLite.Application.Tickets.GetTicketById;

public sealed class GetTicketByIdHandler
{
    private readonly ITicketRepository _repository;

    public GetTicketByIdHandler(ITicketRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
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

        var dto = ticket.ToDetailsDto();
        
        return Result<TicketDetailsDto>.Success(dto);
    }
}
