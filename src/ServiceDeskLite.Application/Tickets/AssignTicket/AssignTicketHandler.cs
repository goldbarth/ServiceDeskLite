using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Application.Common.Validation;
using ServiceDeskLite.Application.Tickets.Audit;
using ServiceDeskLite.Application.Tickets.GetAuditEvents;
using ServiceDeskLite.Application.Tickets.GetTicketById;
using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Domain.Agents;
using ServiceDeskLite.Domain.Common;
using ServiceDeskLite.Domain.Tickets;
using ServiceDeskLite.Domain.Tickets.Events;

namespace ServiceDeskLite.Application.Tickets.AssignTicket;

public sealed class AssignTicketHandler
{
    private readonly ITicketRepository _repository;
    private readonly IAgentRepository _agentRepository;
    private readonly IAuditEventRepository _auditRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICommandValidator<AssignTicketCommand> _validator;
    private readonly IClock _clock;

    public AssignTicketHandler(
        ITicketRepository repository,
        IAgentRepository agentRepository,
        IAuditEventRepository auditRepository,
        IUnitOfWork unitOfWork,
        ICommandValidator<AssignTicketCommand> validator,
        IClock clock)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _auditRepository = auditRepository ?? throw new ArgumentNullException(nameof(auditRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<TicketDetailsDto>> HandleAsync(
        AssignTicketCommand? command,
        CancellationToken ct = default)
    {
        if (command is null)
            return Result<TicketDetailsDto>.Validation(
                "assign_ticket.command.null",
                "Command must not be null.");

        var validation = _validator.Validate(command);
        if (!validation.IsValid)
            return Result<TicketDetailsDto>.ValidationWithFields(
                "assign_ticket.validation_failed",
                "Validation failed.",
                validation.FieldErrors);

        var ticket = await _repository.GetByIdAsync(command.Id, ct);

        if (ticket is null)
            return Result<TicketDetailsDto>.NotFound(
                "ticket.not_found",
                "Ticket was not found.",
                new Dictionary<string, object> { ["ticketId"] = command.Id }!);

        // Resolve the target agent (must exist and be active); null command = unassign.
        Agent? newAgent = null;
        if (command.AgentId is { } agentId)
        {
            newAgent = await _agentRepository.GetByIdAsync(agentId, ct);
            if (newAgent is null || !newAgent.Active)
                return Result<TicketDetailsDto>.Validation(
                    "assign_ticket.agent.unknown",
                    "The agent does not exist or is not active.",
                    new Dictionary<string, object> { ["agentId"] = agentId }!);
        }

        // Snapshot the previous assignee's name for a stable audit trail (ADR-0025).
        var previousName = ticket.AssignedAgentId is { } previousId
            ? (await _agentRepository.GetByIdAsync(previousId, ct))?.Name
            : null;

        try
        {
            ticket.Assign(command.AgentId, newAgent?.Name, previousName);

            var domainEvent = ticket.DomainEvents.OfType<AssigneeChangedDomainEvent>().Single();
            await _auditRepository.AddAsync(
                AuditEventFactory.FromAssigneeChanged(domainEvent, command.Actor, _clock.UtcNow), ct);
            ticket.ClearDomainEvents();

            await _unitOfWork.SaveChangesAsync(ct);

            var auditEvents = await _auditRepository.GetByTicketIdAsync(ticket.Id, ct);
            var auditEventDtos = auditEvents
                .Select(e => new AuditEventDto(e.Id.Value, e.EventType, e.Actor, e.OccurredAt, e.Payload))
                .ToList();

            return Result<TicketDetailsDto>.Success(
                ticket.ToDetailsDto(auditEventDtos, _clock.UtcNow, newAgent?.Name));
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
