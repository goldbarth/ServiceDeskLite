using System.Net;
using System.Text.Json;

using FluentAssertions;

using ServiceDeskLite.Contracts.V1.Tickets;
using ServiceDeskLite.Web.Api.V1;

namespace ServiceDeskLite.Tests.Web.Api;

public class TicketsApiClientTests
{
    private static TicketsApiClient CreateClient(FakeHttpMessageHandler handler)
    {
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost")
        };

        return new TicketsApiClient(http);
    }

    // ───────── 1. Success Case ─────────

    [Fact]
    public async Task GetByIdAsync_Returns_success_when_api_returns_200()
    {
        var ticketId = Guid.NewGuid();
        var body = new TicketResponse(
            ticketId,
            "Test",
            "Desc",
            TicketPriority.Low,
            TicketCategory.Uncategorized,
            TicketStatus.New,
            DateTimeOffset.UtcNow,
            null,
            null,
            [],
            [TicketStatus.Triaged],
            IsOverdue: false,
            DisplayRef: "#ABC1234",
            StatusGuidance: string.Empty,
            SuggestedNextSteps: []);

        var handler = FakeHttpMessageHandler.WithJson(HttpStatusCode.OK, body);
        var client = CreateClient(handler);

        var result = await client.GetByIdAsync(ticketId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be(ticketId);
        result.Value.Title.Should().Be("Test");
    }

    // ───────── 2. ProblemDetails Case ─────────

    [Fact]
    public async Task GetByIdAsync_Returns_failure_when_api_returns_400_with_problem_details()
    {
        var traceId = "abc-123";
        // RFC 9457: extensions are serialized flat at the top level of the problem object.
        // [JsonExtensionData] captures unknown top-level properties, not a nested "extensions" key.
        var problemJson = JsonSerializer.Serialize(new
        {
            type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            title = "Validation Error",
            status = 400,
            detail = "Title is required",
            code = "create_ticket.title.required",
            errorType = "Validation",
            traceId
        });

        var handler = FakeHttpMessageHandler.WithRawJson(HttpStatusCode.BadRequest, problemJson);
        var client = CreateClient(handler);

        var result = await client.GetByIdAsync(Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Status.Should().Be(400);
        result.Error.Code.Should().Be("create_ticket.title.required");
        result.Error.TraceId.Should().Be(traceId);
    }

    // ───────── 3. Unexpected Body ─────────

    [Fact]
    public async Task GetByIdAsync_Returns_failure_when_api_returns_200_with_null_body()
    {
        var handler = FakeHttpMessageHandler.WithRawJson(HttpStatusCode.OK, "null");
        var client = CreateClient(handler);

        var result = await client.GetByIdAsync(Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Status.Should().Be(200);
        result.Error.Title.Should().Be("Invalid server response");
    }

    // ───────── 4. Network Error ─────────

    [Fact]
    public async Task GetByIdAsync_Returns_failure_with_network_error_when_handler_throws()
    {
        var handler = FakeHttpMessageHandler.WithException(
            new HttpRequestException("Connection refused"));
        var client = CreateClient(handler);

        var result = await client.GetByIdAsync(Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Status.Should().Be(0);
        result.Error.Title.Should().Be("Network error");
    }

    // ───────── 5. Cancellation ─────────

    [Fact]
    public async Task GetByIdAsync_Throws_when_cancellation_requested()
    {
        var handler = FakeHttpMessageHandler.WithException(
            new OperationCanceledException());
        var client = CreateClient(handler);

        var act = () => client.GetByIdAsync(Guid.NewGuid(), ct: new CancellationToken(true));

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ───────── UpdateAsync ─────────

    [Fact]
    public async Task UpdateAsync_sends_patch_to_ticket_endpoint_with_partial_body()
    {
        var ticketId = Guid.NewGuid();
        var body = new TicketResponse(
            ticketId, "Updated", "Desc", TicketPriority.High, TicketCategory.Uncategorized, TicketStatus.New,
            DateTimeOffset.UtcNow, null, null, [], [TicketStatus.Triaged],
            IsOverdue: false, DisplayRef: "#ABC1234", StatusGuidance: string.Empty, SuggestedNextSteps: []);

        HttpRequestMessage? captured = null;
        string? capturedContent = null;
        var handler = FakeHttpMessageHandler.Capturing(
            HttpStatusCode.OK, JsonSerializer.Serialize(body),
            (req, content) => { captured = req; capturedContent = content; });
        var client = CreateClient(handler);

        var result = await client.UpdateAsync(ticketId, new UpdateTicketRequest(Priority: TicketPriority.High));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Priority.Should().Be(TicketPriority.High);

        captured!.Method.Should().Be(HttpMethod.Patch);
        captured.RequestUri!.AbsolutePath.Should().Be($"/api/v1/tickets/{ticketId}");
        // Partial update: only the changed field is serialized as a value, others null.
        capturedContent.Should().Contain("\"priority\":\"High\"");
        capturedContent.Should().Contain("\"title\":null");
    }

    [Fact]
    public async Task UpdateAsync_returns_failure_on_problem_details()
    {
        var problemJson = JsonSerializer.Serialize(new
        {
            title = "Conflict",
            status = 409,
            detail = "Closed tickets cannot be updated.",
            code = "ticket.cannot_update_closed",
        });

        var handler = FakeHttpMessageHandler.WithRawJson(HttpStatusCode.Conflict, problemJson);
        var client = CreateClient(handler);

        var result = await client.UpdateAsync(Guid.NewGuid(), new UpdateTicketRequest(Title: "x"));

        result.IsSuccess.Should().BeFalse();
        result.Error!.Status.Should().Be(409);
        result.Error.Code.Should().Be("ticket.cannot_update_closed");
    }

    // ───────── Fake Handler ─────────

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        private FakeHttpMessageHandler(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handler(request, cancellationToken);
        }

        public static FakeHttpMessageHandler WithJson<T>(HttpStatusCode status, T body)
        {
            var json = JsonSerializer.Serialize(body);
            return WithRawJson(status, json);
        }

        public static FakeHttpMessageHandler WithRawJson(HttpStatusCode status, string json)
        {
            return new FakeHttpMessageHandler((_, _) =>
            {
                var response = new HttpResponseMessage(status)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
                return Task.FromResult(response);
            });
        }

        public static FakeHttpMessageHandler Capturing(
            HttpStatusCode status, string json, Action<HttpRequestMessage, string?> capture)
        {
            return new FakeHttpMessageHandler(async (request, ct) =>
            {
                var content = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
                capture(request, content);

                return new HttpResponseMessage(status)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });
        }

        public static FakeHttpMessageHandler WithException(Exception exception)
        {
            return new FakeHttpMessageHandler((_, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                throw exception;
            });
        }
    }
}
