using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Application.Tickets.GetTicketById;
using ServiceDeskLite.Domain.Common;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Application.Tickets.AssignTicket;

public sealed class AssignTicketHandler
{
    private readonly ITicketRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public AssignTicketHandler(ITicketRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<Result<TicketDetailsDto>> HandleAsync(
        AssignTicketCommand? command,
        CancellationToken ct = default)
    {
        if (command is null)
            return Result<TicketDetailsDto>.Validation(
                "assign_ticket.command.null",
                "Command must not be null.");

        var ticket = await _repository.GetByIdAsync(command.Id, ct);

        if (ticket is null)
            return Result<TicketDetailsDto>.NotFound(
                "ticket.not_found",
                "Ticket was not found.",
                new Dictionary<string, object> { ["ticketId"] = command.Id }!);

        try
        {
            // Translate string? → Assignee? – DomainException propagates if name violates invariants
            Assignee? assignee = command.AssigneeName is not null
                ? new Assignee(command.AssigneeName)
                : null;

            ticket.Assign(assignee);
            await _unitOfWork.SaveChangesAsync(ct);

            return Result<TicketDetailsDto>.Success(new TicketDetailsDto(
                ticket.Id,
                ticket.Title,
                ticket.Description,
                ticket.Status,
                ticket.Priority,
                ticket.CreatedAt,
                ticket.DueAt,
                ticket.Assignee));
        }
        catch (DomainException ex) when (ex.Error.Code == TicketErrors.CannotAssignClosedCode)
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
