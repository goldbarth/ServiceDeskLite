using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using FluentAssertions;

using Microsoft.AspNetCore.Hosting;

using ServiceDeskLite.Contracts.V1.Tickets;
using ServiceDeskLite.Tests.Api.Infrastructure;

namespace ServiceDeskLite.Tests.Api.Tickets;

/// <summary>
/// Verifies that audit event payloads are correctly mapped to typed Contracts types
/// and survive a full HTTP serialization roundtrip (API serializes → HTTP → client deserializes).
/// </summary>
public class AuditEventMappingTests
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

    private static HttpClient CreateClient()
        => new ApiWebApplicationFactory()
            .WithWebHostBuilder(b => b.UseEnvironment("Development"))
            .CreateClient();

    private static Task<HttpResponseMessage> PostJsonAsync<T>(HttpClient client, string url, T body)
        => client.PostAsync(url, JsonContent.Create(body, options: SerializeOptions));

    private async Task<Guid> CreateTicketAsync(HttpClient client, string title = "Test Ticket")
    {
        var response = await PostJsonAsync(client, "/api/v1/tickets",
            new CreateTicketRequest(title, "Description", TicketPriority.Medium, null));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<CreateTicketResponse>(DeserializeOptions);
        return created!.Id;
    }

    private async Task<IReadOnlyList<AuditEventResponse>> GetAuditEventsAsync(HttpClient client, Guid ticketId)
    {
        var response = await client.GetAsync($"/api/v1/tickets/{ticketId}/audit-events");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var events = await response.Content.ReadFromJsonAsync<List<AuditEventResponse>>(DeserializeOptions);
        return events!;
    }

    // -----------------------------------------------------------------------
    // ticket.created
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CreateTicket_AuditEvent_HasTicketCreatedPayload()
    {
        var client = CreateClient();
        var ticketId = await CreateTicketAsync(client, title: "Payload Test Ticket");

        var events = await GetAuditEventsAsync(client, ticketId);

        var createdEvent = events.Should().ContainSingle(e => e.EventType == "ticket.created").Subject;
        createdEvent.Payload.Should().BeOfType<TicketCreatedPayload>();

        var payload = (TicketCreatedPayload)createdEvent.Payload;
        payload.Title.Should().Be("Payload Test Ticket");
        payload.Priority.Should().Be("Medium");
    }

    // -----------------------------------------------------------------------
    // ticket.status_changed
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ChangeStatus_AuditEvent_HasStatusChangedPayload()
    {
        var client = CreateClient();
        var ticketId = await CreateTicketAsync(client);

        await PostJsonAsync(client, $"/api/v1/tickets/{ticketId}/status",
            new ChangeTicketStatusRequest(TicketStatus.Triaged));

        var events = await GetAuditEventsAsync(client, ticketId);

        var statusEvent = events.Should().ContainSingle(e => e.EventType == "ticket.status_changed").Subject;
        statusEvent.Payload.Should().BeOfType<TicketStatusChangedPayload>();

        var payload = (TicketStatusChangedPayload)statusEvent.Payload;
        payload.FromStatus.Should().Be("New");
        payload.ToStatus.Should().Be("Triaged");
    }

    // -----------------------------------------------------------------------
    // ticket.comment_added
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AddComment_AuditEvent_HasCommentAddedPayload()
    {
        var client = CreateClient();
        var ticketId = await CreateTicketAsync(client);

        await PostJsonAsync(client, $"/api/v1/tickets/{ticketId}/comments",
            new AddCommentRequest("This is the comment content.", "alice"));

        var events = await GetAuditEventsAsync(client, ticketId);

        var commentEvent = events.Should().ContainSingle(e => e.EventType == "ticket.comment_added").Subject;
        commentEvent.Payload.Should().BeOfType<TicketCommentAddedPayload>();

        var payload = (TicketCommentAddedPayload)commentEvent.Payload;
        payload.Author.Should().Be("alice");
        payload.Content.Should().Be("This is the comment content.");
    }

    [Fact]
    public async Task AddComment_WithoutAuthor_AuditEvent_HasNullAuthorInPayload()
    {
        var client = CreateClient();
        var ticketId = await CreateTicketAsync(client);

        await PostJsonAsync(client, $"/api/v1/tickets/{ticketId}/comments",
            new AddCommentRequest("Anonymous comment.", Author: null));

        var events = await GetAuditEventsAsync(client, ticketId);

        var commentEvent = events.Should().ContainSingle(e => e.EventType == "ticket.comment_added").Subject;
        var payload = commentEvent.Payload.Should().BeOfType<TicketCommentAddedPayload>().Subject;
        payload.Author.Should().BeNull();
        payload.Content.Should().Be("Anonymous comment.");
    }

    // -----------------------------------------------------------------------
    // ticket.assignee_changed
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AssignTicket_AuditEvent_HasAssigneeChangedPayload_WithNewAssignee()
    {
        var client = CreateClient();
        var ticketId = await CreateTicketAsync(client);

        await PostJsonAsync(client, $"/api/v1/tickets/{ticketId}/assign",
            new AssignTicketRequest("bob"));

        var events = await GetAuditEventsAsync(client, ticketId);

        var assignEvent = events.Should().ContainSingle(e => e.EventType == "ticket.assignee_changed").Subject;
        assignEvent.Payload.Should().BeOfType<TicketAssigneeChangedPayload>();

        var payload = (TicketAssigneeChangedPayload)assignEvent.Payload;
        payload.NewAssignee.Should().Be("bob");
        payload.PreviousAssignee.Should().BeNull();
    }

    [Fact]
    public async Task UnassignTicket_AuditEvent_HasAssigneeChangedPayload_WithPreviousAssignee()
    {
        var client = CreateClient();
        var ticketId = await CreateTicketAsync(client);

        await PostJsonAsync(client, $"/api/v1/tickets/{ticketId}/assign",
            new AssignTicketRequest("carol"));
        await PostJsonAsync(client, $"/api/v1/tickets/{ticketId}/assign",
            new AssignTicketRequest(null));

        var events = await GetAuditEventsAsync(client, ticketId);

        var unassignEvent = events
            .Where(e => e.EventType == "ticket.assignee_changed")
            .Should().HaveCount(2)
            .And.Subject.Last();

        var payload = unassignEvent.Payload.Should().BeOfType<TicketAssigneeChangedPayload>().Subject;
        payload.PreviousAssignee.Should().Be("carol");
        payload.NewAssignee.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // ticket.details_updated
    // -----------------------------------------------------------------------

    [Fact]
    public async Task UpdateTicket_AuditEvent_HasDetailsUpdatedPayload_WithOnlyChangedFields()
    {
        var client = CreateClient();
        var ticketId = await CreateTicketAsync(client);

        var response = await client.PatchAsync($"/api/v1/tickets/{ticketId}",
            JsonContent.Create(new UpdateTicketRequest(Priority: TicketPriority.Critical), options: SerializeOptions));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var events = await GetAuditEventsAsync(client, ticketId);

        var updateEvent = events.Should().ContainSingle(e => e.EventType == "ticket.details_updated").Subject;
        var payload = updateEvent.Payload.Should().BeOfType<TicketDetailsUpdatedPayload>().Subject;
        payload.NewPriority.Should().Be("Critical");
        payload.NewTitle.Should().BeNull();
        payload.NewDescription.Should().BeNull();
        payload.NewDueAt.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // Chronological ordering
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AuditEvents_AreReturnedInChronologicalOrder()
    {
        var client = CreateClient();
        var ticketId = await CreateTicketAsync(client);

        await PostJsonAsync(client, $"/api/v1/tickets/{ticketId}/status",
            new ChangeTicketStatusRequest(TicketStatus.Triaged));
        await PostJsonAsync(client, $"/api/v1/tickets/{ticketId}/comments",
            new AddCommentRequest("First comment.", null));

        var events = await GetAuditEventsAsync(client, ticketId);

        events.Should().HaveCount(3);
        events[0].Payload.Should().BeOfType<TicketCreatedPayload>();
        events[1].Payload.Should().BeOfType<TicketStatusChangedPayload>();
        events[2].Payload.Should().BeOfType<TicketCommentAddedPayload>();
        events.Select(e => e.OccurredAt).Should().BeInAscendingOrder();
    }
}