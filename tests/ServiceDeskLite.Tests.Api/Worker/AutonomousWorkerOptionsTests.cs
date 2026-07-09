using FluentAssertions;

using Microsoft.Extensions.Configuration;

using ServiceDeskLite.Api.Assistant;
using ServiceDeskLite.Api.Worker;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Tests.Api.Worker;

/// <summary>
/// The worker's authority is configuration, so the binding is part of the guardrail. A policy that
/// does not mean what it says is worse than no policy.
/// </summary>
public sealed class AutonomousWorkerOptionsTests
{
    private static AutonomousWorkerOptions Bind(params (string Key, string Value)[] settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.ToDictionary(s => s.Key, s => (string?)s.Value))
            .Build();

        var options = new AutonomousWorkerOptions();
        configuration.GetSection(AutonomousWorkerOptions.SectionName).Bind(options);
        options.ApplyDefaults();

        return options;
    }

    [Fact]
    public void An_unconfigured_worker_is_off_and_carries_the_documented_defaults()
    {
        var options = Bind();

        options.Enabled.Should().BeFalse("a background process must not start writing to tickets uninvited");
        options.ScanStatuses.Should().Equal(AutonomousWorkerOptions.DefaultScanStatuses);
        options.AutonomousWrites.Should().Equal(AddCommentTool.Name);
        options.AutonomousStatusTransitions.Should().Equal(TicketStatus.Triaged, TicketStatus.Waiting);
    }

    [Fact]
    public void Configuring_scan_statuses_replaces_the_defaults_rather_than_adding_to_them()
    {
        // The configuration binder appends to a collection that already holds items. With defaults
        // sitting in the property, narrowing the worker's reach to Resolved would have produced
        // [New, Triaged, InProgress, Resolved] — widening it instead. Found in a live run.
        var options = Bind(("AutonomousWorker:ScanStatuses:0", "Resolved"));

        options.ScanStatuses.Should().Equal(TicketStatus.Resolved);
    }

    [Fact]
    public void Configuring_unattended_writes_replaces_the_defaults()
    {
        // The dangerous direction: a deployment that grants assign_ticket must not silently keep
        // add_comment, and one that means to grant only add_comment must not inherit anything else.
        var options = Bind(("AutonomousWorker:AutonomousWrites:0", AssignTicketTool.Name));

        options.AutonomousWrites.Should().Equal(AssignTicketTool.Name);
    }

    [Fact]
    public void Configuring_status_transitions_replaces_the_defaults()
    {
        var options = Bind(
            ("AutonomousWorker:AutonomousStatusTransitions:0", "Triaged"),
            ("AutonomousWorker:AutonomousStatusTransitions:1", "Resolved"));

        options.AutonomousStatusTransitions.Should().Equal(TicketStatus.Triaged, TicketStatus.Resolved);
        options.AutonomousStatusTransitions.Should().NotContain(TicketStatus.Waiting);
    }

    [Fact]
    public void Scalar_settings_bind_as_written()
    {
        var options = Bind(
            ("AutonomousWorker:Enabled", "true"),
            ("AutonomousWorker:ScanIntervalSeconds", "60"),
            ("AutonomousWorker:MaxTicketsPerRun", "3"),
            ("AutonomousWorker:MinTicketAgeMinutes", "0"));

        options.Enabled.Should().BeTrue();
        options.ScanIntervalSeconds.Should().Be(60);
        options.MaxTicketsPerRun.Should().Be(3);
        options.MinTicketAgeMinutes.Should().Be(0);
    }

    [Fact]
    public void Applying_defaults_twice_changes_nothing()
    {
        // PostConfigure runs once, but an options instance that reconfigures must not accumulate.
        var options = Bind(("AutonomousWorker:ScanStatuses:0", "Resolved"));
        options.ApplyDefaults();

        options.ScanStatuses.Should().Equal(TicketStatus.Resolved);
    }
}
