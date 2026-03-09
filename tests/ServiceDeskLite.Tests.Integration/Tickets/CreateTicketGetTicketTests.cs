using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using FluentAssertions;

using ServiceDeskLite.Contracts.V1.Tickets;

namespace ServiceDeskLite.Tests.Integration.Tickets;

public sealed class CreateTicketGetTicketTests
{
    private static readonly DateTimeOffset FixedDueAt = new(2026, 03, 12, 10, 00, 00, TimeSpan.Zero);

    private static readonly JsonSerializerOptions SerializeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task CreateTicket_ThenGetTicket_ReturnsPersistedTicket()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var request = new CreateTicketRequest(
            Title: "VPN reconnect loop",
            Description: "User gets disconnected every 5 minutes.",
            Priority: TicketPriority.High,
            DueAt: FixedDueAt);

        var createResponse = await client.PostAsync(
            "/api/v1/tickets",
            JsonContent.Create(request, options: SerializeOptions));

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await createResponse.Content
            .ReadFromJsonAsync<CreateTicketResponse>(DeserializeOptions);

        created.Should().NotBeNull();
        created!.Id.Should().NotBe(Guid.Empty);
        createResponse.Headers.Location.Should().NotBeNull();
        createResponse.Headers.Location!.ToString().Should().EndWith($"/api/v1/tickets/{created.Id}");

        var getResponse = await client.GetAsync($"/api/v1/tickets/{created.Id}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var ticket = await getResponse.Content.ReadFromJsonAsync<TicketResponse>(DeserializeOptions);

        ticket.Should().NotBeNull();
        ticket!.Id.Should().Be(created.Id);
        ticket.Title.Should().Be(request.Title);
        ticket.Description.Should().Be(request.Description);
        ticket.Priority.Should().Be("High");
        ticket.Status.Should().Be("New");
        ticket.DueAt.Should().Be(request.DueAt);
        ticket.Assignee.Should().BeNull();
        ticket.Comments.Should().BeEmpty();
        ticket.CreatedAt.Should().NotBe(default);
    }
}
