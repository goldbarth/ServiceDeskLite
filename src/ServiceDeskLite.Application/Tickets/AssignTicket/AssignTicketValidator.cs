using ServiceDeskLite.Application.Common.Validation;

namespace ServiceDeskLite.Application.Tickets.AssignTicket;

public sealed class AssignTicketValidator : ICommandValidator<AssignTicketCommand>
{
    // Assignment is by agent id now: null = unassign, otherwise the handler validates
    // that the agent exists and is active (a lookup the validator cannot perform).
    // No structural field rules remain.
    public FieldValidationResult Validate(AssignTicketCommand command)
        => new FieldValidationBuilder().Build();
}
