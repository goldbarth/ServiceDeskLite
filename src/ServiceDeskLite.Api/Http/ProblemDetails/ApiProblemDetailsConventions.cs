namespace ServiceDeskLite.Api.Http.ProblemDetails;

public static class ApiProblemDetailsConventions
{
    public static class Titles
    {
        public const string Validation = "Validation failed";
        public const string NotFound = "Resource not found";
        public const string Conflict = "Conflict";
        public const string Unexpected = "Unexpected error";
        public const string BadRequest = "Bad request";
    }
}
