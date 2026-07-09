using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using ServiceDeskLite.Api.Assistant.Sandbox;

namespace ServiceDeskLite.Tests.Api.Assistant.Sandbox;

/// <summary>
/// The pipeline's whole reason to exist is the check-then-commit split: a call refused by any
/// guard must cost nothing anywhere. Without it, whichever guard ran first would spend its
/// allowance on a call that never executes.
/// </summary>
public sealed class ToolGuardPipelineTests
{
    private static ToolGuardPipeline Pipeline(params IToolGuard[] guards)
        => new(guards, NullLogger<ToolGuardPipeline>.Instance);

    [Fact]
    public void Admits_a_call_no_guard_refuses()
    {
        var guard = new SpyGuard(allow: true);

        var result = Pipeline(guard).Admit(SandboxTestContext.Context("create_ticket"));

        result.IsAllowed.Should().BeTrue();
        guard.Commits.Should().Be(1);
    }

    [Fact]
    public void Returns_the_first_refusal_and_its_reason()
    {
        var result = Pipeline(
                new SpyGuard(allow: true),
                new SpyGuard(allow: false, reason: "first refusal"),
                new SpyGuard(allow: false, reason: "second refusal"))
            .Admit(SandboxTestContext.Context("create_ticket"));

        result.IsAllowed.Should().BeFalse();
        result.Reason.Should().Be("first refusal");
    }

    [Fact]
    public void A_refusal_commits_nothing_not_even_on_guards_that_already_admitted()
    {
        var admitting = new SpyGuard(allow: true);
        var refusing = new SpyGuard(allow: false, reason: "no");

        Pipeline(admitting, refusing).Admit(SandboxTestContext.Context("create_ticket"));

        admitting.Checks.Should().Be(1);
        admitting.Commits.Should().Be(0, "a budget must not be spent on a call that never runs");
    }

    [Fact]
    public void Stops_checking_after_the_first_refusal()
    {
        var refusing = new SpyGuard(allow: false, reason: "no");
        var later = new SpyGuard(allow: true);

        Pipeline(refusing, later).Admit(SandboxTestContext.Context("create_ticket"));

        later.Checks.Should().Be(0);
    }

    [Fact]
    public void Commits_every_guard_once_all_of_them_admitted()
    {
        var first = new SpyGuard(allow: true);
        var second = new SpyGuard(allow: true);

        Pipeline(first, second).Admit(SandboxTestContext.Context("create_ticket"));

        first.Commits.Should().Be(1);
        second.Commits.Should().Be(1);
    }

    private sealed class SpyGuard(bool allow, string? reason = null) : IToolGuard
    {
        public int Checks { get; private set; }
        public int Commits { get; private set; }

        public ToolGuardResult Check(ToolInvocationContext context)
        {
            Checks++;
            return allow ? ToolGuardResult.Allow() : ToolGuardResult.Deny(reason!);
        }

        public void Commit(ToolInvocationContext context) => Commits++;
    }
}
