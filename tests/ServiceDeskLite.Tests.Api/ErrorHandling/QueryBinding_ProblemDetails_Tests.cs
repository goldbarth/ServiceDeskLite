using System.Net;
using System.Net.Http.Json;

using FluentAssertions;

using Microsoft.AspNetCore.Mvc;

using ServiceDeskLite.Tests.Api.Infrastructure;

namespace ServiceDeskLite.Tests.Api.ErrorHandling;

public class QueryBinding_ProblemDetails_Tests
{
    [Fact]
    public async Task InvalidQueryEnumBinding_Returns400_ProblemDetails()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            "/api/v1/tickets?page=1&pageSize=25&sortField=NoSuchField&sortDirection=Desc");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType.Should().NotBeNull();
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be((int)HttpStatusCode.BadRequest);
        problem.Extensions.Should().ContainKey("code");
        problem.Extensions["code"]!.ToString().Should().Be("api.request.bad_request");
        problem.Extensions.Should().ContainKey("errorType");
        problem.Extensions["errorType"]!.ToString().Should().Be("validation");
        problem.Extensions.Should().ContainKey("traceId");
        problem.Detail.Should().BeNull();
    }
}
