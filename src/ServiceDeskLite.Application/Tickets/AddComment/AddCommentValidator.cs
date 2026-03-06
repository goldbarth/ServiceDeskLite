using ServiceDeskLite.Application.Common.Validation;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Application.Tickets.AddComment;

public sealed class AddCommentValidator : ICommandValidator<AddCommentCommand>
{
    public FieldValidationResult Validate(AddCommentCommand command)
    {
        var builder = new FieldValidationBuilder();

        if (string.IsNullOrWhiteSpace(command.Content))
            builder.AddError("content", "Content is required.");
        else if (command.Content.Length > Comment.MaxContentLength)
            builder.AddError("content", $"Must not exceed {Comment.MaxContentLength} characters.");

        if (command.Author is not null && command.Author.Length > Comment.MaxAuthorLength)
            builder.AddError("author", $"Must not exceed {Comment.MaxAuthorLength} characters.");

        return builder.Build();
    }
}
