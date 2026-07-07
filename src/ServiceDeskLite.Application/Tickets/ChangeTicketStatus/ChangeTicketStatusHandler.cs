using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Application.Tickets.Audit;
using ServiceDeskLite.Application.Tickets.GetAuditEvents;
using ServiceDeskLite.Application.Tickets.GetTicketById;
using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Domain.Common;
using ServiceDeskLite.Domain.Tickets;
using ServiceDeskLite.Domain.Tickets.Events;

namespace ServiceDeskLite.Application.Tickets.ChangeTicketStatus;

public sealed class ChangeTicketStatusHandler
{
    private readonly ITicketRepository _repository;
    private readonly IAgentRepository _agentRepository;
    private readonly IAuditEventRepository _auditRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ChangeTicketStatusHandler(
        ITicketRepository repository,
        IAgentRepository agentRepository,
        IAuditEventRepository auditRepository,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _auditRepository = auditRepository ?? throw new ArgumentNullException(nameof(auditRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
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

            var domainEvent = ticket.DomainEvents.OfType<StatusChangedDomainEvent>().Single();
            await _auditRepository.AddAsync(
                AuditEventFactory.FromStatusChanged(domainEvent, command.Actor, _clock.UtcNow), ct);
            ticket.ClearDomainEvents();

            await _unitOfWork.SaveChangesAsync(ct);

            var auditEvents = await _auditRepository.GetByTicketIdAsync(ticket.Id, ct);
            var auditEventDtos = auditEvents
                .Select(e => new AuditEventDto(e.Id.Value, e.EventType, e.Actor, e.OccurredAt, e.Payload))
                .ToList();

            var assigneeName = ticket.AssignedAgentId is { } agentId
                ? (await _agentRepository.GetByIdAsync(agentId, ct))?.Name
                : null;

            return Result<TicketDetailsDto>.Success(
                ticket.ToDetailsDto(auditEventDtos, _clock.UtcNow, assigneeName));
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
