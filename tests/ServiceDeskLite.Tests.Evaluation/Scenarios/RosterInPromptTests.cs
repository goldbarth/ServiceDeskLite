using System.Text.Json;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using ServiceDeskLite.Application.Agents.Seeding;
using ServiceDeskLite.Tests.Evaluation.Harness;

namespace ServiceDeskLite.Tests.Evaluation.Scenarios;

/// <summary>
/// The agent roster reaches the model up front, not only through a failed assign_ticket call
/// (issue #193). Discovery-by-failure meant the valid names entered the transcript only inside an
/// error message, where the model paraphrased them and invented an agent ("Morgan Davis" for
/// Morgan Diaz). The scripted handler records the request the loop sends upstream, so the
/// assertion reads the system prompt the model actually received.
/// </summary>
public sealed class RosterInPromptTests
{
    private static string SystemPromptOf(JsonElement request) =>
        request.GetProperty("system").GetString() ?? string.Empty;

    [Fact]
    public async Task The_system_prompt_names_every_seeded_agent()
    {
        using var host = new EvaluationHost();

        // The host runs as Production, where startup seeding is off; seed the roster the same
        // way Program does, through the provider-agnostic seeder.
        using (var scope = host.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IAgentSeeder>().SeedAsync();

        host.Model.Then(ScriptedTurn.Create().Says("Hello."));

        await host.CreateClient().ChatAsync("Hi.");

        var prompt = SystemPromptOf(host.Model.Requests[0]);

        // The seeded roster, by exact name - including the one the model got wrong in the field.
        prompt.Should().ContainAll("Alex Kim", "Sam Rivera", "Priya Nair", "Jordan Lee", "Morgan Diaz");
        prompt.Should().NotContain("Morgan Davis", "the invented name must have no source to echo");
    }

    [Fact]
    public async Task An_empty_roster_is_stated_rather_than_omitted()
    {
        // No seeding: the prompt must still address assignment honestly instead of leaving the
        // model to fall back on invented names.
        using var host = new EvaluationHost();
        host.Model.Then(ScriptedTurn.Create().Says("Hello."));

        await host.CreateClient().ChatAsync("Hi.");

        SystemPromptOf(host.Model.Requests[0]).Should().Contain("roster is currently empty");
    }
}
