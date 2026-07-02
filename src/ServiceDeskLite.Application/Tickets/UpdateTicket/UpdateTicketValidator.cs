using ServiceDeskLite.Application.Common.Validation;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Application.Tickets.UpdateTicket;

public sealed class UpdateTicketValidator : ICommandValidator<UpdateTicketCommand>
{
    public FieldValidationResult Validate(UpdateTicketCommand command)
    {
        var builder = new FieldValidationBuilder();

        if (command.Title is null
            && command.Description is null
            && command.Priority is null
            && command.DueAt is null)
        {
            builder.AddError("command", "At least one field must be provided.");
        }

        if (command.Title is not null)
        {
            if (string.IsNullOrWhiteSpace(command.Title))
                builder.AddError("title", "Title must not be blank.");
            else if (command.Title.Length > Ticket.MaxTitleLength)
                builder.AddError("title", $"Must not exceed {Ticket.MaxTitleLength} characters.");
        }

        if (command.Description is not null)
        {
            if (string.IsNullOrWhiteSpace(command.Description))
                builder.AddError("description", "Description must not be blank.");
            else if (command.Description.Length > Ticket.MaxDescriptionLength)
                builder.AddError("description", $"Must not exceed {Ticket.MaxDescriptionLength} characters.");
        }

        return builder.Build();
    }
}
