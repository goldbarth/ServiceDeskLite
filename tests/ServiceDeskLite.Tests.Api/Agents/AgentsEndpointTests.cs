using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using FluentAssertions;

using Microsoft.AspNetCore.Hosting;

using ServiceDeskLite.Contracts.V1.Agents;
using ServiceDeskLite.Tests.Api.Infrastructure;

namespace ServiceDeskLite.Tests.Api.Agents;

public class AgentsEndpointTests
{
    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static HttpClient CreateClient()
        => new ApiWebApplicationFactory()
            .WithWebHostBuilder(b => b.UseEnvironment("Development"))
            .CreateClient();

    [Fact]
    public async Task GetAgents_ReturnsSeededActiveRoster()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/v1/agents");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var agents = await response.Content.ReadFromJsonAsync<List<AgentResponse>>(DeserializeOptions);

        agents.Should().NotBeNullOrEmpty();
        agents!.Should().OnlyContain(a => !string.IsNullOrWhiteSpace(a.Name) && a.Id != Guid.Empty);
        agents.Select(a => a.Name).Should().BeInAscendingOrder("the roster is ordered by name");
    }
}
