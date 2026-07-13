using FluentAssertions;

using ServiceDeskLite.Api.Assistant;
using ServiceDeskLite.Application.Tickets.GetTicketById;
using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Tests.Api.Assistant;

/// <summary>
/// Summary timestamps are expressed in the user's timezone (issue #194). The field symptom: a
/// comment written at 12:07 local appeared in the summary as 10:07 - the model faithfully
/// repeated the UTC times it was fed while the UI rendered local times right next to it. The
/// zone is pinned here, so the assertion is exact regardless of where the test runs.
/// </summary>
public sealed class TicketSummaryPromptTests
{
    // Fixed zone and a summer date, so the CEST offset (+02:00) is part of the assertion.
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    private static TicketDetailsDto Ticket(
        DateTimeOffset createdAt, DateTimeOffset? dueAt = null, IReadOnlyList<ConversationItemDto>? conversation = null) =>
        new(
            Id: new TicketId(Guid.NewGuid()),
            Title: "Printer offline",
            Description: "Third floor printer is dead.",
            Status: TicketStatus.New,
            Priority: TicketPriority.High,
            Category: TicketCategory.Hardware,
            CreatedAt: createdAt,
            DueAt: dueAt,
            Assignee: null,
            Conversation: conversation ?? [],
            AllowedTransitions: [TicketStatus.Triaged],
            IsOverdue: false,
            DisplayRef: "#ABC123",
            StatusGuidance: string.Empty,
            SuggestedNextSteps: []);

    [Fact]
    public void Timestamps_are_rendered_in_the_user_timezone_not_utc()
    {
        // The exact field case: written at 10:07 UTC = 12:07 in Berlin (CEST).
        var comment = new ConversationItemDto(
            Timestamp: new DateTimeOffset(2026, 7, 10, 10, 7, 0, TimeSpan.Zero),
            Kind: ConversationItemKind.Comment,
            Comment: new CommentDto(
                new CommentId(Guid.NewGuid()), "Rebooted it, no change.", "Alex Kim",
                new DateTimeOffset(2026, 7, 10, 10, 7, 0, TimeSpan.Zero)),
            Event: null);

        var prompt = TicketSummaryService.BuildTicketPrompt(
            Ticket(
                createdAt: new DateTimeOffset(2026, 7, 10, 6, 30, 0, TimeSpan.Zero),
                dueAt: new DateTimeOffset(2026, 7, 15, 15, 0, 0, TimeSpan.Zero),
                conversation: [comment]),
            Berlin);

        prompt.Should().Contain("2026-07-10 12:07", "the comment was written at 12:07 local time");
        prompt.Should().NotContain("10:07", "the UTC wall time must not leak into the prompt");
        prompt.Should().Contain("2026-07-10 08:30", "created 06:30 UTC is 08:30 in Berlin");
        prompt.Should().Contain("2026-07-15 17:00", "due 15:00 UTC is 17:00 in Berlin");
        prompt.Should().Contain("+02:00", "the offset stays visible so the model expresses dueAt with it");
    }

    [Fact]
    public void The_prompt_names_the_zone_the_timestamps_are_in()
    {
        var prompt = TicketSummaryService.BuildTicketPrompt(
            Ticket(createdAt: DateTimeOffset.UtcNow), Berlin);

        prompt.Should().Contain("Europe/Berlin",
            "the model must know which zone it is reading, not just see an offset");
    }
}
