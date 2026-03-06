using ServiceDeskLite.Application.Common.Validation;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Application.Tickets.CreateTicket;

public sealed class CreateTicketValidator : ICommandValidator<CreateTicketCommand>
{
    public FieldValidationResult Validate(CreateTicketCommand command)
    {
        var builder = new FieldValidationBuilder();

        if (string.IsNullOrWhiteSpace(command.Title))
            builder.AddError("title", "Title is required.");
        else if (command.Title.Length > Ticket.MaxTitleLength)
            builder.AddError("title", $"Must not exceed {Ticket.MaxTitleLength} characters.");

        if (string.IsNullOrWhiteSpace(command.Description))
            builder.AddError("description", "Description is required.");
        else if (command.Description.Length > Ticket.MaxDescriptionLength)
            builder.AddError("description", $"Must not exceed {Ticket.MaxDescriptionLength} characters.");

        return builder.Build();
    }
}