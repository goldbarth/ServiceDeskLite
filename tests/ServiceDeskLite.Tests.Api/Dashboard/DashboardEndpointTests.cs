using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using FluentAssertions;

using Microsoft.AspNetCore.Hosting;

using ServiceDeskLite.Contracts.V1.Dashboard;
using ServiceDeskLite.Contracts.V1.Tickets;
using ServiceDeskLite.Tests.Api.Infrastructure;

namespace ServiceDeskLite.Tests.Api.Dashboard;

public class DashboardEndpointTests
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    // Creates a factory with InMemory provider (Development) so no SQLite is needed.
    private static ApiWebApplicationFactory CreateFactory()
        => new ApiWebApplicationFactory()
            .WithWebHostBuilder(b => b.UseEnvironment("Development")) as ApiWebApplicationFactory
           ?? throw new InvalidOperationException("Cast failed.");

    [Fact]
    public async Task GetSummary_Returns200Ok_WithNonNegativeCounts()
    {
        using var factory = new ApiWebApplicationFactory()
            .WithWebHostBuilder(b => b.UseEnvironment("Development"));
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/dashboard/summary");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var summary = await response.Content
            .ReadFromJsonAsync<DashboardSummaryResponse>(_options);

        summary.Should().NotBeNull();
        summary!.NewCount.Should().BeGreaterThanOrEqualTo(0);
        summary.TriagedCount.Should().BeGreaterThanOrEqualTo(0);
        summary.InProgressCount.Should().BeGreaterThanOrEqualTo(0);
        summary.OverdueCount.Should().BeGreaterThanOrEqualTo(0);
        summary.ResolvedLast7DaysCount.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetSummary_AfterCreatingNewTicket_NewCountIncreasesByOne()
    {
        using var factory = new ApiWebApplicationFactory()
            .WithWebHostBuilder(b => b.UseEnvironment("Development"));
        var client = factory.CreateClient();

        // Establish baseline before creating a ticket.
        var baseline = await GetSummaryAsync(client);

        await client.PostAsJsonAsync("/api/v1/tickets",
            new CreateTicketRequest("Dashboard test ticket", "Body", TicketPriority.Low, null),
            _options);

        var after = await GetSummaryAsync(client);

        after.NewCount.Should().Be(baseline.NewCount + 1);
    }

    private static async Task<DashboardSummaryResponse> GetSummaryAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/dashboard/summary");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<DashboardSummaryResponse>(_options))!;
    }
}