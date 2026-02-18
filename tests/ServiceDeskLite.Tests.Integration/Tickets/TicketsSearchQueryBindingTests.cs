using System.Net;
using System.Net.Http.Json;

using FluentAssertions;

using Microsoft.AspNetCore.Mvc;

namespace ServiceDeskLite.Tests.Integration.Tickets;

public sealed class TicketsSearchQueryBindingTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public TicketsSearchQueryBindingTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SearchTickets_WithValidSortFieldAndDirection_ReturnsOk()
    {
        // Arrange
        var url = "/api/v1/tickets?page=1&pageSize=25&sortField=CreatedAt&sortDirection=Desc";

        // Act
        var response = await _client.GetAsync(url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SearchTickets_WithInvalidSortField_ReturnsBadRequest_WithProblemDetails()
    {
        // Arrange
        var url = "/api/v1/tickets?page=1&pageSize=25&sortField=NoSuchField&sortDirection=Desc";

        // Act
        var response = await _client.GetAsync(url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Minimal API binding failures for invalid enum values go through ApiExceptionHandler
        // and return a generic ProblemDetails (not ValidationProblemDetails with field errors).
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be((int)HttpStatusCode.BadRequest);
        problem.Extensions.Should().ContainKey("code");
        problem.Extensions.Should().ContainKey("errorType");
    }
}
