namespace ServiceDeskLite.Application.Common.Validation;

public sealed class FieldValidationBuilder
{
    private readonly Dictionary<string, List<string>> _errors = [];

    public FieldValidationBuilder AddError(string field, string message)
    {
        if (!_errors.TryGetValue(field, out var list))
            _errors[field] = list = [];

        list.Add(message);
        return this;
    }

    public FieldValidationResult Build()
        => _errors.Count == 0
            ? FieldValidationResult.Ok
            : FieldValidationResult.Failure(
                _errors.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.ToArray()));
}
