using System.Net;
using FluentAssertions;

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
    public async Task SearchTickets_WithInvalidSortField_ReturnsBadRequest()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        // Arrange
        var url = "/api/v1/tickets?page=1&pageSize=25&sortField=NoSuchField&sortDirection=Desc";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
