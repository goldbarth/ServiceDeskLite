using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using FluentAssertions;

using Microsoft.AspNetCore.Hosting;

using ServiceDeskLite.Contracts.V1.Tickets;
using ServiceDeskLite.Tests.Api.Infrastructure;

namespace ServiceDeskLite.Tests.Api.Tickets;

public class ChangeTicketStatusEndpointTests
{
    // Use string enum serialization to match the API's JsonStringEnumConverter expectation
    private static readonly JsonSerializerOptions _serializeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly JsonSerializerOptions _deserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static HttpClient CreateClient()
        => new ApiWebApplicationFactory()
            .WithWebHostBuilder(b => b.UseEnvironment("Development"))
            .CreateClient();

    private static Task<HttpResponseMessage> PostJsonAsync<T>(HttpClient client, string url, T value)
        => client.PostAsync(url, JsonContent.Create(value, options: _serializeOptions));

    [Fact]
    public async Task ChangeStatus_ValidTransition_Returns200WithUpdatedTicket()
    {
        var client = CreateClient();

        var createResponse = await PostJsonAsync(client, "/api/v1/tickets",
            new CreateTicketRequest("Test", "Description", TicketPriority.Medium, null));

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateTicketResponse>(_deserializeOptions);

        var statusResponse = await PostJsonAsync(client,
            $"/api/v1/tickets/{created!.Id}/status",
            new ChangeTicketStatusRequest(TicketStatus.Triaged));

        statusResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var ticket = await statusResponse.Content.ReadFromJsonAsync<TicketResponse>(_deserializeOptions);
        ticket!.Status.Should().Be("Triaged");
        ticket.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task ChangeStatus_TicketNotFound_Returns404()
    {
        var client = CreateClient();

        var response = await PostJsonAsync(client,
            $"/api/v1/tickets/{Guid.NewGuid()}/status",
            new ChangeTicketStatusRequest(TicketStatus.Triaged));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ChangeStatus_InvalidTransition_Returns409WithErrorCode()
    {
        var client = CreateClient();

        var createResponse = await PostJsonAsync(client, "/api/v1/tickets",
            new CreateTicketRequest("Test", "Description", TicketPriority.Low, null));

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateTicketResponse>(_deserializeOptions);

        // New → Closed is not an allowed transition
        var response = await PostJsonAsync(client,
            $"/api/v1/tickets/{created!.Id}/status",
            new ChangeTicketStatusRequest(TicketStatus.Closed));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var pd = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>(
            _deserializeOptions);

        pd!.Extensions.Should().ContainKey("code");
        pd.Extensions["code"]!.ToString().Should().Be("domain.ticket.status.invalid_transition");
    }

    [Fact]
    public async Task ChangeStatus_SameStatus_Returns409()
    {
        var client = CreateClient();

        var createResponse = await PostJsonAsync(client, "/api/v1/tickets",
            new CreateTicketRequest("Test", "Description", TicketPriority.Low, null));

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateTicketResponse>(_deserializeOptions);

        // New → New is not an allowed transition
        var response = await PostJsonAsync(client,
            $"/api/v1/tickets/{created!.Id}/status",
            new ChangeTicketStatusRequest(TicketStatus.New));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
