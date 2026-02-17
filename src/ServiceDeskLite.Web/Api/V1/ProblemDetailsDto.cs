using System.Text.Json;

namespace ServiceDeskLite.Web.Api.V1;

public class ProblemDetailsDto
{
    public string? Type { get; init; }
    public string? Title { get; init; }
    public int? Status { get; init; }
    public string? Detail { get; init; }
    public string? Instance { get; init; }

    public Dictionary<string, JsonElement>? Extensions { get; init; }
}
