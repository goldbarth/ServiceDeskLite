namespace ServiceDeskLite.Application.Common.Validation;

public sealed class FieldValidationResult
{
    public static readonly FieldValidationResult Ok = new(new Dictionary<string, string[]>());

    public IReadOnlyDictionary<string, string[]> FieldErrors { get; }
    public bool IsValid => FieldErrors.Count == 0;

    private FieldValidationResult(IReadOnlyDictionary<string, string[]> errors)
        => FieldErrors = errors;

    public static FieldValidationResult Failure(IReadOnlyDictionary<string, string[]> errors)
        => new(errors);
}
