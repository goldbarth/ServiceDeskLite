using System.Net;
using System.Text;

using FluentAssertions;

using ServiceDeskLite.Contracts.V1.Assistant;
using ServiceDeskLite.Web.Api.V1.Assistant;

namespace ServiceDeskLite.Tests.Web.Api;

/// <summary>
/// Guards the wire contract of the summary stream: the section name crosses the
/// API/Web boundary as a JSON string, and the client must stop at the terminal event
/// instead of hanging on an open stream.
/// </summary>
public sealed class TicketSummaryApiClientTests
{
    private static TicketSummaryApiClient CreateClient(string sse, HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new FakeSseHandler(sse, status);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };

        return new TicketSummaryApiClient(http);
    }

    private static async Task<List<TicketSummaryStreamEvent>> DrainAsync(TicketSummaryApiClient client)
    {
        List<TicketSummaryStreamEvent> events = [];
        await foreach (var evt in client.StreamAsync(Guid.NewGuid()))
            events.Add(evt);

        return events;
    }

    [Fact]
    public async Task Deserializes_every_section_name_from_the_wire()
    {
        var sse = string.Concat(Enum.GetValues<TicketSummarySection>().Select(s =>
            $"event: delta\ndata: {{\"section\":\"{s}\",\"text\":\"x\"}}\n\n"));

        var events = await DrainAsync(CreateClient(sse + "event: done\ndata: {}\n\n"));

        events.Where(e => e.EventType == TicketSummaryStreamEvent.DeltaEvent)
            .Select(e => e.Section)
            .Should().Equal(Enum.GetValues<TicketSummarySection>().Cast<TicketSummarySection?>());
    }

    [Fact]
    public async Task Yields_deltas_then_stops_at_the_done_event()
    {
        const string sse =
            "event: delta\ndata: {\"section\":\"Summary\",\"text\":\"Hello \"}\n\n" +
            "event: delta\ndata: {\"section\":\"Summary\",\"text\":\"world\"}\n\n" +
            "event: done\ndata: {}\n\n" +
            "event: delta\ndata: {\"section\":\"Risks\",\"text\":\"never read\"}\n\n";

        var events = await DrainAsync(CreateClient(sse));

        events.Should().HaveCount(3, "the stream must end at 'done' and ignore anything after it");
        events[^1].EventType.Should().Be(TicketSummaryStreamEvent.DoneEvent);
        string.Concat(events.Where(e => e.Text is not null).Select(e => e.Text))
            .Should().Be("Hello world");
    }

    [Fact]
    public async Task Stops_at_an_error_event()
    {
        const string sse =
            "event: delta\ndata: {\"section\":\"Summary\",\"text\":\"partial\"}\n\n" +
            "event: error\ndata: {\"message\":\"The AI service is currently unavailable.\"}\n\n";

        var events = await DrainAsync(CreateClient(sse));

        events[^1].EventType.Should().Be(TicketSummaryStreamEvent.ErrorEvent);
        events[^1].Message.Should().Be("The AI service is currently unavailable.");
    }

    [Fact]
    public async Task Surfaces_a_problem_details_response_as_a_single_error_event()
    {
        const string problem =
            """{"title":"Resource not found","detail":"Ticket was not found.","status":404}""";

        var events = await DrainAsync(CreateClient(problem, HttpStatusCode.NotFound));

        events.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new
            {
                EventType = TicketSummaryStreamEvent.ErrorEvent,
                Message = "Ticket was not found.",
            });
    }

    private sealed class FakeSseHandler(string body, HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var contentType = status == HttpStatusCode.OK ? "text/event-stream" : "application/problem+json";

            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, contentType),
            });
        }
    }
}
