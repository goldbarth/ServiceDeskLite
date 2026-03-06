using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Application.Common.Validation;
using ServiceDeskLite.Application.Tickets.Audit;
using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Domain.Common;
using ServiceDeskLite.Domain.Tickets.Events;

namespace ServiceDeskLite.Application.Tickets.AddComment;

public sealed class AddCommentHandler
{
    private readonly ITicketRepository _repository;
    private readonly IAuditEventRepository _auditRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICommandValidator<AddCommentCommand> _validator;

    public AddCommentHandler(
        ITicketRepository repository,
        IAuditEventRepository auditRepository,
        IUnitOfWork unitOfWork,
        ICommandValidator<AddCommentCommand> validator)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _auditRepository = auditRepository ?? throw new ArgumentNullException(nameof(auditRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    public async Task<Result<AddCommentResult>> HandleAsync(
        AddCommentCommand? command,
        CancellationToken ct = default)
    {
        if (command is null)
            return Result<AddCommentResult>.Validation(
                "add_comment.command.null",
                "Command must not be null.");

        var validation = _validator.Validate(command);
        if (!validation.IsValid)
            return Result<AddCommentResult>.ValidationWithFields(
                "add_comment.validation_failed",
                "Validation failed.",
                validation.FieldErrors);

        var ticket = await _repository.GetByIdAsync(command.TicketId, ct);

        if (ticket is null)
            return Result<AddCommentResult>.NotFound(
                "ticket.not_found",
                "Ticket was not found.",
                new Dictionary<string, object> { ["ticketId"] = command.TicketId }!);

        try
        {
            var comment = ticket.AddComment(command.Content, command.CreatedAt, command.Author);

            var domainEvent = ticket.DomainEvents.OfType<CommentAddedDomainEvent>().Single();
            await _auditRepository.AddAsync(
                AuditEventFactory.FromCommentAdded(domainEvent, command.CreatedAt), ct);
            ticket.ClearDomainEvents();

            await _unitOfWork.SaveChangesAsync(ct);

            return Result<AddCommentResult>.Success(
                new AddCommentResult(new CommentDto(comment.Id, comment.Content, comment.Author, comment.CreatedAt)));
        }
        catch (DomainException ex)
        {
            return Result<AddCommentResult>.Failure(DomainExceptionMapper.ToApplicationError(ex));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<AddCommentResult>.Failure(PersistenceExceptionMapper.ToApplicationError(ex));
        }
    }
}
