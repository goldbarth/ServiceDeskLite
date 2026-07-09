using FluentAssertions;

using ServiceDeskLite.Application.Abstractions.Assistant;
using ServiceDeskLite.Domain.Audit;
using ServiceDeskLite.Domain.Tickets;
using ServiceDeskLite.Infrastructure.InMemory.Persistence;

namespace ServiceDeskLite.Tests.Infrastructure.InMemory;

/// <summary>
/// Covers what the aggregation cannot: the window boundary, and the fact that the
/// automation rate is read off the audit actor rather than from anything the assistant
/// reports about itself.
/// </summary>
public sealed class InMemoryAiDashboardRepositoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Since = Now.AddDays(-7);

    private readonly InMemoryStore _store = new();

    private InMemoryAiDashboardRepository CreateSut() => new(_store);

    private InMemoryAssistantMetricsSink CreateSink() => new(_store);

    private void SeedAudit(string? actor, DateTimeOffset occurredAt)
        => _store.AppendAuditEvents(
        [
            new AuditEvent(
                AuditEventId.New(), TicketId.New(), AuditEventTypes.TicketCreated, actor, occurredAt, "{}"),
        ]);

    [Fact]
    public async Task Automation_rate_counts_only_audit_events_authored_by_the_assistant()
    {
        SeedAudit(AuditActors.AiAssistant, Now.AddDays(-1));
        SeedAudit(AuditActors.AiAssistant, Now.AddDays(-2));
        SeedAudit("felix", Now.AddDays(-1));
        SeedAudit(null, Now.AddDays(-1));

        var result = await CreateSut().GetAsync(Since, Now);

        result.Automation.AiActions.Should().Be(2);
        result.Automation.TotalActions.Should().Be(4);
        result.Automation.Rate.Should().Be(0.5);
    }

    [Fact]
    public async Task Events_and_metrics_outside_the_window_are_excluded()
    {
        SeedAudit(AuditActors.AiAssistant, Now.AddDays(-8));

        var sink = CreateSink();
        await sink.RecordToolInvocationAsync(
            new AssistantToolInvocation(
                "create_ticket", AssistantToolKind.Action, false, null, null, null,
                TimeSpan.FromMilliseconds(5), Now.AddDays(-9)),
            CancellationToken.None);
        await sink.RecordTokenUsageAsync(
            new AssistantTokenUsage("claude-opus-4-8", 100, 50, Now.AddDays(-9)), CancellationToken.None);

        var result = await CreateSut().GetAsync(Since, Now);

        result.Automation.TotalActions.Should().Be(0);
        result.Tools.Should().BeEmpty();
        result.Tokens.ModelTurns.Should().Be(0);
        result.Tokens.TotalTokens.Should().Be(0);
    }

    [Fact]
    public async Task Token_usage_sums_every_model_turn_in_the_window()
    {
        var sink = CreateSink();
        await sink.RecordTokenUsageAsync(
            new AssistantTokenUsage("claude-opus-4-8", 1_000, 200, Now.AddDays(-1)), CancellationToken.None);
        await sink.RecordTokenUsageAsync(
            new AssistantTokenUsage("claude-opus-4-8", 1_500, 300, Now.AddHours(-1)), CancellationToken.None);

        var result = await CreateSut().GetAsync(Since, Now);

        result.Tokens.ModelTurns.Should().Be(2);
        result.Tokens.InputTokens.Should().Be(2_500);
        result.Tokens.OutputTokens.Should().Be(500);
        result.Tokens.TotalTokens.Should().Be(3_000);
    }

    [Fact]
    public async Task Window_days_is_derived_from_the_range_the_numbers_actually_cover()
    {
        var result = await CreateSut().GetAsync(Now.AddDays(-30), Now);

        result.WindowDays.Should().Be(30);
    }

    [Fact]
    public async Task Volume_reports_all_tickets_and_those_created_in_the_window()
    {
        _store.ApplyAdds(
        [
            new Ticket(TicketId.New(), "Old", "Description", TicketPriority.Medium, Now.AddDays(-40)),
            new Ticket(TicketId.New(), "Recent", "Description", TicketPriority.Medium, Now.AddDays(-2)),
        ]);

        var result = await CreateSut().GetAsync(Since, Now);

        result.Volume.TotalTickets.Should().Be(2);
        result.Volume.CreatedInWindow.Should().Be(1);
    }

    [Fact]
    public async Task An_untouched_deployment_reports_unknown_rates_rather_than_zeroes()
    {
        var result = await CreateSut().GetAsync(Since, Now);

        result.Automation.Rate.Should().BeNull();
        result.Retrieval.DuplicateRate.Should().BeNull();
        result.Retrieval.AverageConfidence.Should().BeNull();
        result.Retrieval.SemanticAvailable.Should().BeFalse();
    }
}
