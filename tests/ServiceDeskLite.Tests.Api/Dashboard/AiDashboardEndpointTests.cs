using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using FluentAssertions;

using Microsoft.AspNetCore.Hosting;

using ServiceDeskLite.Contracts.V1.Dashboard;
using ServiceDeskLite.Tests.Api.Infrastructure;

namespace ServiceDeskLite.Tests.Api.Dashboard;

public sealed class AiDashboardEndpointTests
{
    private static readonly JsonSerializerOptions _options = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task GetAiMetrics_Returns200Ok_WithTheTrailingWindow()
    {
        using var factory = new ApiWebApplicationFactory()
            .WithWebHostBuilder(b => b.UseEnvironment("Development"));

        var response = await factory.CreateClient().GetAsync("/api/v1/dashboard/ai");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var metrics = await response.Content.ReadFromJsonAsync<AiDashboardResponse>(_options);

        metrics.Should().NotBeNull();
        metrics!.WindowDays.Should().Be(7);
        metrics.Volume.TotalTickets.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetAiMetrics_WithoutAssistantActivity_ReportsNullRatesRatherThanZero()
    {
        // A fresh host has seeded tickets but no assistant runs. Reporting 0 % here would
        // read as "the assistant did nothing useful" instead of "nothing has been measured".
        using var factory = new ApiWebApplicationFactory()
            .WithWebHostBuilder(b => b.UseEnvironment("Development"));

        var metrics = await factory.CreateClient()
            .GetFromJsonAsync<AiDashboardResponse>("/api/v1/dashboard/ai", _options);

        metrics.Should().NotBeNull();
        metrics!.Retrieval.DuplicateChecks.Should().Be(0);
        metrics.Retrieval.DuplicateRate.Should().BeNull();
        metrics.Retrieval.AverageConfidence.Should().BeNull();
        metrics.Retrieval.SemanticAvailable.Should().BeFalse("the InMemory provider has no semantic search");
        metrics.Tools.Should().BeEmpty();
        metrics.Tokens.ModelTurns.Should().Be(0);
    }

    [Fact]
    public async Task GetAiMetrics_RequiresTheApiKey()
    {
        using var factory = new ApiWebApplicationFactory()
            .WithWebHostBuilder(b => b.UseEnvironment("Development"));

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Remove("X-Api-Key");

        var response = await client.GetAsync("/api/v1/dashboard/ai");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
