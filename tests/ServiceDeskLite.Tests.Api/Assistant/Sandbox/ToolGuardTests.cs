using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using ServiceDeskLite.Api.Assistant;
using ServiceDeskLite.Api.Assistant.Sandbox;
using ServiceDeskLite.Application.Abstractions.Assistant;

namespace ServiceDeskLite.Tests.Api.Assistant.Sandbox;

public sealed class KnownToolGuardTests
{
    [Fact]
    public void Admits_every_tool_in_the_catalog()
    {
        var guard = new KnownToolGuard();

        foreach (var tool in ToolCatalog.Kinds.Keys)
            guard.Check(SandboxTestContext.Context(tool)).IsAllowed.Should().BeTrue(tool);
    }

    [Fact]
    public void Refuses_a_name_that_is_not_in_the_catalog()
    {
        var result = new KnownToolGuard().Check(SandboxTestContext.Context("delete_everything"));

        result.IsAllowed.Should().BeFalse();
        result.Reason.Should().Contain("delete_everything");
    }
}

public sealed class InputSizeGuardTests
{
    private static InputSizeGuard Guard(int maxInput = 200, int maxString = 50)
        => new(SandboxTestContext.Options(new AgentSandboxOptions
        {
            MaxInputCharacters = maxInput,
            MaxStringCharacters = maxString,
        }));

    [Fact]
    public void Admits_ordinary_arguments()
    {
        var result = Guard().Check(SandboxTestContext.Context("create_ticket", """{"title":"Printer down"}"""));

        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void Refuses_arguments_whose_raw_json_exceeds_the_limit()
    {
        var input = $$"""{"description":"{{new string('x', 400)}}"}""";

        var result = Guard(maxInput: 100, maxString: 10_000).Check(
            SandboxTestContext.Context("create_ticket", input));

        result.IsAllowed.Should().BeFalse();
        result.Reason.Should().Contain("too large");
    }

    [Fact]
    public void Refuses_an_oversized_string_and_names_the_property()
    {
        var input = $$"""{"title":"ok","description":"{{new string('x', 60)}}"}""";

        var result = Guard().Check(SandboxTestContext.Context("create_ticket", input));

        result.IsAllowed.Should().BeFalse();
        result.Reason.Should().Contain("description", "the model can only fix what it can identify");
    }

    [Fact]
    public void Finds_an_oversized_string_nested_in_an_array()
    {
        var input = $$"""{"statuses":["New","{{new string('x', 60)}}"]}""";

        var result = Guard().Check(SandboxTestContext.Context("find_similar_tickets", input));

        result.IsAllowed.Should().BeFalse();
        result.Reason.Should().Contain("statuses[1]");
    }

    [Fact]
    public void Ignores_non_string_values()
    {
        var result = Guard().Check(SandboxTestContext.Context("find_similar_tickets", """{"limit":5,"flag":true}"""));

        result.IsAllowed.Should().BeTrue();
    }
}

public sealed class WriteBudgetGuardTests
{
    private static WriteBudgetGuard Guard(int maxWrites = 2)
        => new(SandboxTestContext.Options(new AgentSandboxOptions { MaxWritesPerTurn = maxWrites }));

    [Fact]
    public void Retrievals_never_count_against_the_budget()
    {
        var guard = Guard(maxWrites: 1);
        var turn = new ToolTurnState();

        for (var i = 0; i < 10; i++)
        {
            var context = SandboxTestContext.Context(SearchTicketsTool.Name, turn: turn);
            guard.Check(context).IsAllowed.Should().BeTrue();
            guard.Commit(context);
        }

        turn.Writes.Should().Be(0);
    }

    [Fact]
    public void Refuses_a_write_once_the_turn_budget_is_spent()
    {
        var guard = Guard(maxWrites: 2);
        var turn = new ToolTurnState();

        for (var i = 0; i < 2; i++)
        {
            var allowed = SandboxTestContext.Context(CreateTicketTool.Name, turn: turn);
            guard.Check(allowed).IsAllowed.Should().BeTrue();
            guard.Commit(allowed);
        }

        var refused = guard.Check(SandboxTestContext.Context(AssignTicketTool.Name, turn: turn));

        refused.IsAllowed.Should().BeFalse();
        refused.Reason.Should().Contain("limit");
    }

    [Fact]
    public void A_retrieval_is_still_admitted_after_the_write_budget_is_spent()
    {
        var guard = Guard(maxWrites: 1);
        var turn = new ToolTurnState();

        var write = SandboxTestContext.Context(CreateTicketTool.Name, turn: turn);
        guard.Commit(write);

        guard.Check(SandboxTestContext.Context(SearchTicketsTool.Name, turn: turn))
            .IsAllowed.Should().BeTrue("a spent write budget must not silence the model's reads");
    }

    [Fact]
    public void Check_alone_spends_nothing()
    {
        var guard = Guard(maxWrites: 1);
        var turn = new ToolTurnState();

        guard.Check(SandboxTestContext.Context(CreateTicketTool.Name, turn: turn));
        guard.Check(SandboxTestContext.Context(CreateTicketTool.Name, turn: turn));

        turn.Writes.Should().Be(0);
    }
}

public sealed class RateLimitGuardTests
{
    private static (RateLimitGuard Guard, FakeClock Clock) Create(int perMinute)
    {
        var clock = new FakeClock();
        var options = SandboxTestContext.Options(new AgentSandboxOptions { ToolCallsPerMinute = perMinute });
        var guard = new RateLimitGuard(new TokenBucketRegistry(clock), options, NullLogger<RateLimitGuard>.Instance);

        return (guard, clock);
    }

    [Fact]
    public void Refuses_once_the_bucket_is_empty()
    {
        var (guard, _) = Create(perMinute: 2);

        for (var i = 0; i < 2; i++)
        {
            var context = SandboxTestContext.Context(SearchTicketsTool.Name);
            guard.Check(context).IsAllowed.Should().BeTrue();
            guard.Commit(context);
        }

        var refused = guard.Check(SandboxTestContext.Context(SearchTicketsTool.Name));

        refused.IsAllowed.Should().BeFalse();
        refused.Reason.Should().Contain("rate limited");
    }

    [Fact]
    public void Check_alone_consumes_no_token()
    {
        var (guard, _) = Create(perMinute: 1);

        guard.Check(SandboxTestContext.Context(SearchTicketsTool.Name));
        guard.Check(SandboxTestContext.Context(SearchTicketsTool.Name));

        guard.Check(SandboxTestContext.Context(SearchTicketsTool.Name))
            .IsAllowed.Should().BeTrue("only Commit spends the budget");
    }

    [Fact]
    public void Refills_over_time()
    {
        var (guard, clock) = Create(perMinute: 60);

        for (var i = 0; i < 60; i++)
            guard.Commit(SandboxTestContext.Context(SearchTicketsTool.Name));

        guard.Check(SandboxTestContext.Context(SearchTicketsTool.Name)).IsAllowed.Should().BeFalse();

        // 60 per minute refills one token per second.
        clock.Advance(TimeSpan.FromSeconds(1));

        guard.Check(SandboxTestContext.Context(SearchTicketsTool.Name)).IsAllowed.Should().BeTrue();
    }
}

public sealed class ToolCatalogTests
{
    [Fact]
    public void Every_tool_the_model_is_offered_is_in_the_catalog()
    {
        // A tool wired into the dispatch but missing here would be refused at runtime by
        // KnownToolGuard. Catching that in a test beats catching it in a conversation.
        var toolNames = typeof(CreateTicketTool).Assembly
            .GetTypes()
            .Where(t => t.Name.EndsWith("Tool", StringComparison.Ordinal))
            .Select(t => t.GetField("Name")?.GetRawConstantValue() as string)
            .Where(name => name is not null)
            .ToList();

        toolNames.Should().HaveCount(11);
        toolNames.Should().OnlyContain(name => ToolCatalog.IsKnown(name!));
    }

    [Fact]
    public void Only_state_changing_tools_count_as_writes()
    {
        ToolCatalog.IsWrite(CreateTicketTool.Name).Should().BeTrue();
        ToolCatalog.IsWrite(RouteTicketTool.Name).Should().BeTrue();
        ToolCatalog.IsWrite(RememberTool.Name).Should().BeTrue();

        ToolCatalog.IsWrite(SearchTicketsTool.Name).Should().BeFalse();
        ToolCatalog.IsWrite(FindSimilarTicketsTool.Name).Should().BeFalse();
        ToolCatalog.IsWrite(CheckGroundingTool.Name).Should().BeFalse();
    }

    [Fact]
    public void Kinds_match_what_the_dashboard_aggregates_on()
    {
        ToolCatalog.KindOf(FindSimilarTicketsTool.Name).Should().Be(AssistantToolKind.DuplicateCheck);
        ToolCatalog.KindOf(CheckGroundingTool.Name).Should().Be(AssistantToolKind.Evaluation);
        ToolCatalog.KindOf(SearchKnowledgeBaseTool.Name).Should().Be(AssistantToolKind.Retrieval);
        ToolCatalog.KindOf(CreateTicketTool.Name).Should().Be(AssistantToolKind.Action);
    }
}
