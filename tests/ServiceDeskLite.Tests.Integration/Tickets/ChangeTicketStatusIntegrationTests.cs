using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using FluentAssertions;

using Microsoft.AspNetCore.Mvc;

using ServiceDeskLite.Contracts.V1.Tickets;

namespace ServiceDeskLite.Tests.Integration.Tickets;

public sealed class ChangeTicketStatusIntegrationTests
{
    private static readonly JsonSerializerOptions SerializeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task ChangeStatus_ValidTransition_ThenGetTicket_ReturnsUpdatedStatus()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var ticketId = await CreateTicketAsync(client);

        var changeResponse = await client.PostAsync(
            $"/api/v1/tickets/{ticketId}/status",
            JsonContent.Create(
                new ChangeTicketStatusRequest(TicketStatus.Triaged),
                options: SerializeOptions));

        changeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await changeResponse.Content.ReadFromJsonAsync<TicketResponse>(DeserializeOptions);
        updated.Should().NotBeNull();
        updated!.Id.Should().Be(ticketId);
        updated.Status.Should().Be(TicketStatus.Triaged);
        updated.AllowedTransitions.Should().Equal(TicketStatus.InProgress, TicketStatus.Waiting, TicketStatus.Resolved);

        var getResponse = await client.GetAsync($"/api/v1/tickets/{ticketId}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var ticket = await getResponse.Content.ReadFromJsonAsync<TicketResponse>(DeserializeOptions);
        ticket.Should().NotBeNull();
        ticket!.Id.Should().Be(ticketId);
        ticket.Status.Should().Be(TicketStatus.Triaged);
        ticket.AllowedTransitions.Should().Equal(TicketStatus.InProgress, TicketStatus.Waiting, TicketStatus.Resolved);
    }

    [Fact]
    public async Task ChangeStatus_InvalidTransition_ReturnsConflict_AndDoesNotChangeTicket()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var ticketId = await CreateTicketAsync(client);

        var changeResponse = await client.PostAsync(
            $"/api/v1/tickets/{ticketId}/status",
            JsonContent.Create(
                new ChangeTicketStatusRequest(TicketStatus.Closed),
                options: SerializeOptions));

        changeResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problem = await changeResponse.Content.ReadFromJsonAsync<ProblemDetails>(DeserializeOptions);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be((int)HttpStatusCode.Conflict);
        problem.Extensions.Should().ContainKey("code");
        problem.Extensions["code"]!.ToString().Should().Be("domain.ticket.status.invalid_transition");

        var getResponse = await client.GetAsync($"/api/v1/tickets/{ticketId}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var ticket = await getResponse.Content.ReadFromJsonAsync<TicketResponse>(DeserializeOptions);
        ticket.Should().NotBeNull();
        ticket!.Id.Should().Be(ticketId);
        ticket.Status.Should().Be(TicketStatus.New);
        ticket.AllowedTransitions.Should().Equal(TicketStatus.Triaged);
    }

    private static async Task<Guid> CreateTicketAsync(HttpClient client)
    {
        var createResponse = await client.PostAsync(
            "/api/v1/tickets",
            JsonContent.Create(
                new CreateTicketRequest(
                    Title: "Status test ticket",
                    Description: "Used for change status integration tests.",
                    Priority: TicketPriority.Medium,
                    DueAt: null),
                options: SerializeOptions));

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await createResponse.Content.ReadFromJsonAsync<CreateTicketResponse>(DeserializeOptions);
        created.Should().NotBeNull();

        return created!.Id;
    }
}
