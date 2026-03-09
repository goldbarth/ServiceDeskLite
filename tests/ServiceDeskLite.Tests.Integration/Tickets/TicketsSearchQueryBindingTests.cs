using System.Net;
using System.Net.Http.Json;

using FluentAssertions;

using Microsoft.AspNetCore.Mvc;

namespace ServiceDeskLite.Tests.Integration.Tickets;

public sealed class TicketsSearchQueryBindingTests
{
    [Fact]
    public async Task SearchTickets_WithValidSortFieldAndDirection_ReturnsOk()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        // Arrange
        var url = "/api/v1/tickets?page=1&pageSize=25&sortField=CreatedAt&sortDirection=Desc";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SearchTickets_WithInvalidSortField_ReturnsBadRequest_WithProblemDetails()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        // Arrange
        var url = "/api/v1/tickets?page=1&pageSize=25&sortField=NoSuchField&sortDirection=Desc";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

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
