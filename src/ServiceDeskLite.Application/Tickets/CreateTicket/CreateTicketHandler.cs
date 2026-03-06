using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Application.Common.Validation;
using ServiceDeskLite.Application.Tickets.Audit;
using ServiceDeskLite.Domain.Common;
using ServiceDeskLite.Domain.Tickets;
using ServiceDeskLite.Domain.Tickets.Events;

namespace ServiceDeskLite.Application.Tickets.CreateTicket;

public sealed class CreateTicketHandler
{
    private readonly ITicketRepository _repository;
    private readonly IAuditEventRepository _auditRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICommandValidator<CreateTicketCommand> _validator;

    public CreateTicketHandler(
        ITicketRepository repo,
        IAuditEventRepository auditRepository,
        IUnitOfWork unitOfWork,
        ICommandValidator<CreateTicketCommand> validator)
    {
        _repository = repo ?? throw new ArgumentNullException(nameof(repo));
        _auditRepository = auditRepository ?? throw new ArgumentNullException(nameof(auditRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    public async Task<Result<CreateTicketResult>> HandleAsync(
        CreateTicketCommand? command,
        CancellationToken ct = default)
    {
        if (command is null)
            return Result<CreateTicketResult>.Validation(
                "create_ticket.command.null",
                "Command must not be null.");

        var validation = _validator.Validate(command);
        if (!validation.IsValid)
            return Result<CreateTicketResult>.ValidationWithFields(
                "create_ticket.validation_failed",
                "Validation failed.",
                validation.FieldErrors);

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

            // Translate the domain event raised by the constructor into an audit record.
            // Both are queued before SaveChangesAsync so they are persisted atomically.
            var domainEvent = ticket.DomainEvents.OfType<TicketCreatedDomainEvent>().Single();
            await _auditRepository.AddAsync(
                AuditEventFactory.FromTicketCreated(domainEvent, command.Actor, command.CreatedAt), ct);
            ticket.ClearDomainEvents();

            await _unitOfWork.SaveChangesAsync(ct);

            return Result<CreateTicketResult>.Success(new CreateTicketResult(ticket.Id));
        }
        catch (DomainException ex)
        {
            return Result<CreateTicketResult>.Failure(DomainExceptionMapper.ToApplicationError(ex));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<CreateTicketResult>.Failure(PersistenceExceptionMapper.ToApplicationError(ex));
        }
    }
}
