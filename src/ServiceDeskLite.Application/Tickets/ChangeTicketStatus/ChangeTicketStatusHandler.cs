using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Application.Tickets.GetTicketById;
using ServiceDeskLite.Domain.Common;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Application.Tickets.ChangeTicketStatus;

public sealed class ChangeTicketStatusHandler
{
    private readonly ITicketRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ChangeTicketStatusHandler(ITicketRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<Result<TicketDetailsDto>> HandleAsync(
        ChangeTicketStatusCommand? command,
        CancellationToken ct = default)
    {
        if (command is null)
            return Result<TicketDetailsDto>.Validation(
                "change_ticket_status.command.null",
                "Command must not be null.");

        var ticket = await _repository.GetByIdAsync(command.Id, ct);

        if (ticket is null)
            return Result<TicketDetailsDto>.NotFound(
                "ticket.not_found",
                "Ticket was not found.",
                new Dictionary<string, object> { ["ticketId"] = command.Id }!);

        try
        {
            ticket.ChangeStatus(command.NewStatus);
            await _unitOfWork.SaveChangesAsync(ct);

            return Result<TicketDetailsDto>.Success(new TicketDetailsDto(
                ticket.Id,
                ticket.Title,
                ticket.Description,
                ticket.Status,
                ticket.Priority,
                ticket.CreatedAt,
                ticket.DueAt));
        }
        catch (DomainException ex) when (ex.Error.Code == TicketErrors.InvalidTransitionCode)
        {
            return Result<TicketDetailsDto>.Conflict(ex.Error.Code, ex.Error.Message);
        }
        catch (DomainException ex)
        {
            return Result<TicketDetailsDto>.Failure(DomainExceptionMapper.ToApplicationError(ex));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<TicketDetailsDto>.Failure(PersistenceExceptionMapper.ToApplicationError(ex));
        }
    }
}
