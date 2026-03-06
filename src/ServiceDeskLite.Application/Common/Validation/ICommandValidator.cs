namespace ServiceDeskLite.Application.Common.Validation;

public interface ICommandValidator<TCommand>
{
    FieldValidationResult Validate(TCommand command);
}