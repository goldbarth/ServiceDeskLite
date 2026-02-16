using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Domain.Common;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Application.Tickets.CreateTicket;

public sealed class CreateTicketHandler
{
    private readonly ITicketRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateTicketHandler(ITicketRepository repo, IUnitOfWork unitOfWork)
    {
        _repository = repo ?? throw new ArgumentNullException(nameof(repo));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<Result<CreateTicketResult>> HandleAsync(
        CreateTicketCommand? command,
        CancellationToken ct = default)
    {
        if (command is null)
            return Result<CreateTicketResult>.Validation(
                "create_ticket.command.null",
                "Command must not be null.");

        if (string.IsNullOrWhiteSpace(command.Title))
            return Result<CreateTicketResult>.Validation(
                "create_ticket.title.required",
                "Title is required.");

        if (string.IsNullOrWhiteSpace(command.Description))
            return Result<CreateTicketResult>.Validation(
                "create_ticket.description.required",
                "Description is required.");

        try
        {
            var id = TicketId.New();

            var ticket = new Ticket(
                id,
                command.Title,
                command.Description,
                command.Priority,
                command.CreatedAt,
                command.DueAt);
            
            await _repository.AddAsync(ticket, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            
            return Result<CreateTicketResult>.Success(new CreateTicketResult(ticket.Id));
        }
        catch (DomainException ex)
        {
            return  Result<CreateTicketResult>.Failure(DomainExceptionMapper.ToApplicationError(ex));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<CreateTicketResult>.Failure(PersistenceExceptionMapper.ToApplicationError(ex));
        }
    }
}
