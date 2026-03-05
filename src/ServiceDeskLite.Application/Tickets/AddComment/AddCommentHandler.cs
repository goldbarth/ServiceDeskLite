using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Domain.Common;

namespace ServiceDeskLite.Application.Tickets.AddComment;

public sealed class AddCommentHandler
{
    private readonly ITicketRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public AddCommentHandler(ITicketRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<Result<AddCommentResult>> HandleAsync(
        AddCommentCommand? command,
        CancellationToken ct = default)
    {
        if (command is null)
            return Result<AddCommentResult>.Validation(
                "add_comment.command.null",
                "Command must not be null.");

        var ticket = await _repository.GetByIdAsync(command.TicketId, ct);

        if (ticket is null)
            return Result<AddCommentResult>.NotFound(
                "ticket.not_found",
                "Ticket was not found.",
                new Dictionary<string, object> { ["ticketId"] = command.TicketId }!);

        try
        {
            var comment = ticket.AddComment(command.Content, command.CreatedAt, command.Author);
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
