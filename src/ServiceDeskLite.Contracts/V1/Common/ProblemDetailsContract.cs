namespace ServiceDeskLite.Contracts.V1.Common;

public static class ProblemDetailsContract
{
    public static class Extensions
    {
        public const string Code = "code";
        public const string ErrorType = "errorType";
        public const string Meta = "meta";
        public const string TraceId = "traceId";
    }

    public static class ErrorTypes
    {
        public const string Validation = "validation";
        public const string NotFound = "not_found";
        public const string Conflict = "conflict";
        public const string DomainViolation = "domain_violation";
        public const string Unexpected = "unexpected";
    }
}
