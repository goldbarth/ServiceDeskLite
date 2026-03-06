using ServiceDeskLite.Application.Common.Validation;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Application.Tickets.AssignTicket;

public sealed class AssignTicketValidator : ICommandValidator<AssignTicketCommand>
{
    public FieldValidationResult Validate(AssignTicketCommand command)
    {
        var builder = new FieldValidationBuilder();

        // null means "unassign" – always valid; only validate when a name is provided
        if (command.AssigneeName is not null)
        {
            if (string.IsNullOrWhiteSpace(command.AssigneeName))
                builder.AddError("assigneeName", "AssigneeName must not be empty when provided.");
            else if (command.AssigneeName.Length > Assignee.MaxNameLength)
                builder.AddError("assigneeName", $"Must not exceed {Assignee.MaxNameLength} characters.");
        }

        return builder.Build();
    }
}