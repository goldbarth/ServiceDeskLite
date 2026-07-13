using FluentAssertions;

using ServiceDeskLite.Contracts.V1.Tickets;
using ServiceDeskLite.Web.Features.Tickets.State;

namespace ServiceDeskLite.Tests.Web.Features.Tickets.State;

/// <summary>
/// The segmented control derives "active" from these two methods; if Apply and Matches
/// disagree, a preset either never lights up or stays lit while the list shows
/// something else.
/// </summary>
public sealed class TicketViewPresetsTests
{
    public static TheoryData<TicketViewPreset> Presets()
    {
        var data = new TheoryData<TicketViewPreset>();
        foreach (var preset in TicketViewPresets.AllPresets)
            data.Add(preset);
        return data;
    }

    [Theory]
    [MemberData(nameof(Presets))]
    public void ApplyingAPresetMakesItMatch(TicketViewPreset preset)
    {
        var applied = preset.Apply(TicketQueryParams.Default);

        preset.Matches(applied).Should().BeTrue();
        TicketViewPresets.AllPresets.Count(p => p.Matches(applied)).Should().Be(1,
            "presets must be mutually exclusive or two segments light up at once");
    }

    [Fact]
    public void ApplyKeepsSortAndPageSizeButResetsPageAndFilters()
    {
        var current = TicketQueryParams.Default with
        {
            Page = 3,
            PageSize = 100,
            SortField = TicketSortField.DueAt,
            Q = "printer",
            Assignee = "Alex",
        };

        var applied = TicketViewPresets.Unassigned.Apply(current);

        applied.Page.Should().Be(1);
        applied.PageSize.Should().Be(100);
        applied.SortField.Should().Be(TicketSortField.DueAt);
        applied.Q.Should().BeNull();
        applied.Assignee.Should().BeNull();
        applied.Unassigned.Should().BeTrue();
    }

    [Fact]
    public void AnEditedFilterDeselectsThePreset()
    {
        var applied = TicketViewPresets.SlaCritical.Apply(TicketQueryParams.Default);

        TicketViewPresets.SlaCritical.Matches(applied with { Q = "vpn" }).Should().BeFalse();
        TicketViewPresets.SlaCritical.Matches(applied with { Priorities = [TicketPriority.High] }).Should().BeFalse();
        TicketViewPresets.SlaCritical.Matches(applied with { Overdue = false }).Should().BeFalse();
    }

    [Fact]
    public void MatchesIgnoresPagingAndSort()
    {
        var applied = TicketViewPresets.Open.Apply(TicketQueryParams.Default) with
        {
            Page = 7,
            PageSize = 50,
            SortField = TicketSortField.Priority,
        };

        TicketViewPresets.Open.Matches(applied).Should().BeTrue();
    }

    [Fact]
    public void OpenMatchesStatusesRegardlessOfOrder()
    {
        var reordered = TicketQueryParams.Default with
        {
            Statuses =
            [
                TicketStatus.Waiting, TicketStatus.New, TicketStatus.InProgress, TicketStatus.Triaged,
            ],
        };

        TicketViewPresets.Open.Matches(reordered).Should().BeTrue();
    }

    [Fact]
    public void SearchRequestCarriesThePresetFlags()
    {
        var unassigned = TicketViewPresets.Unassigned.Apply(TicketQueryParams.Default).ToSearchRequest();
        unassigned.Unassigned.Should().BeTrue();
        unassigned.Overdue.Should().BeNull("an absent flag must not serialize at all");

        var overdue = TicketViewPresets.SlaCritical.Apply(TicketQueryParams.Default).ToSearchRequest();
        overdue.Overdue.Should().BeTrue();
        overdue.Unassigned.Should().BeNull();
    }
}
