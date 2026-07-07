using System.Text.Json;

using FluentAssertions;

using ServiceDeskLite.Api.Assistant;
using ServiceDeskLite.Application.Tickets.SearchTickets;
using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Tests.Api.Assistant;

public sealed class SearchTicketsToolInputTests
{
    private static JsonElement Json(string json) => JsonSerializer.Deserialize<JsonElement>(json);

    [Fact]
    public void TryParseInput_EmptyObject_ListsAllWithDefaults()
    {
        var ok = SearchTicketsTool.TryParseInput(Json("{ }"), out var query, out var error);

        ok.Should().BeTrue();
        error.Should().BeNull();
        query!.Criteria.Text.Should().BeNull();
        query.Criteria.Statuses.Should().BeNull();
        query.Criteria.Priorities.Should().BeNull();
        query.Criteria.AssigneeName.Should().BeNull();
        query.Sort.Should().BeNull();
        query.Paging.Page.Should().Be(1);
        query.Paging.PageSize.Should().Be(10);
    }

    [Fact]
    public void TryParseInput_AllFilters_MapsCriteria()
    {
        var input = Json("""
            {
              "query": "login",
              "status": ["New", "InProgress"],
              "priority": ["High", "Critical"],
              "assignee": "Alex",
              "sortBy": "Priority",
              "sortDirection": "Asc",
              "limit": 5
            }
            """);

        var ok = SearchTicketsTool.TryParseInput(input, out var query, out var error);

        ok.Should().BeTrue();
        error.Should().BeNull();
        query!.Criteria.Text.Should().Be("login");
        query.Criteria.Statuses.Should().BeEquivalentTo(new[] { TicketStatus.New, TicketStatus.InProgress });
        query.Criteria.Priorities.Should().BeEquivalentTo(new[] { TicketPriority.High, TicketPriority.Critical });
        query.Criteria.AssigneeName.Should().Be("Alex");
        query.Sort.Should().Be(new SortSpec(TicketSortField.Priority, SortDirection.Asc));
        query.Paging.PageSize.Should().Be(5);
    }

    [Fact]
    public void TryParseInput_EnumsAreCaseInsensitive()
    {
        var input = Json("""{ "status": ["new"], "priority": ["high"] }""");

        var ok = SearchTicketsTool.TryParseInput(input, out var query, out _);

        ok.Should().BeTrue();
        query!.Criteria.Statuses.Should().ContainSingle().Which.Should().Be(TicketStatus.New);
        query.Criteria.Priorities.Should().ContainSingle().Which.Should().Be(TicketPriority.High);
    }

    [Fact]
    public void TryParseInput_BlankTextAndAssignee_NormalizeToNull()
    {
        var input = Json("""{ "query": "  ", "assignee": "" }""");

        var ok = SearchTicketsTool.TryParseInput(input, out var query, out _);

        ok.Should().BeTrue();
        query!.Criteria.Text.Should().BeNull();
        query.Criteria.AssigneeName.Should().BeNull();
    }

    [Fact]
    public void TryParseInput_EmptyStatusArray_LeavesFilterNull()
    {
        var input = Json("""{ "status": [] }""");

        var ok = SearchTicketsTool.TryParseInput(input, out var query, out _);

        ok.Should().BeTrue();
        query!.Criteria.Statuses.Should().BeNull();
    }

    [Fact]
    public void TryParseInput_OnlySortDirection_DefaultsField()
    {
        var input = Json("""{ "sortDirection": "Asc" }""");

        var ok = SearchTicketsTool.TryParseInput(input, out var query, out _);

        ok.Should().BeTrue();
        query!.Sort.Should().Be(new SortSpec(SortSpec.Default.Field, SortDirection.Asc));
    }

    [Fact]
    public void TryParseInput_InvalidStatus_Fails()
    {
        var input = Json("""{ "status": ["Unknown"] }""");

        var ok = SearchTicketsTool.TryParseInput(input, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("status");
    }

    [Fact]
    public void TryParseInput_StatusNotArray_Fails()
    {
        var input = Json("""{ "status": "New" }""");

        var ok = SearchTicketsTool.TryParseInput(input, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("status");
    }

    [Fact]
    public void TryParseInput_InvalidSortBy_Fails()
    {
        var input = Json("""{ "sortBy": "Nonsense" }""");

        var ok = SearchTicketsTool.TryParseInput(input, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("sortBy");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(26)]
    [InlineData(-1)]
    public void TryParseInput_LimitOutOfRange_Fails(int limit)
    {
        var input = Json($$"""{ "limit": {{limit}} }""");

        var ok = SearchTicketsTool.TryParseInput(input, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("limit");
    }

    [Fact]
    public void TryParseInput_NonObjectInput_Fails()
    {
        var ok = SearchTicketsTool.TryParseInput(Json("\"just a string\""), out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("JSON object");
    }

    [Fact]
    public void FormatResult_WithItems_ListsOnePerLineNoBodies()
    {
        var id = new TicketId(Guid.Parse("11111111-2222-3333-4444-555555555555"));
        var dto = new TicketListItemDto(
            Id: id,
            Title: "Cannot log in",
            Status: TicketStatus.InProgress,
            Priority: TicketPriority.High,
            CreatedAt: DateTimeOffset.UtcNow,
            DueAt: null,
            Assignee: "Alex",
            AllowedTransitions: [],
            IsOverdue: false,
            DisplayRef: "TCK-1");
        var result = new SearchTicketsResult(new PagedResult<TicketListItemDto>([dto], TotalCount: 3, Paging.Default));

        var text = SearchTicketsTool.FormatResult(result);

        text.Should().Contain("Found 3 matching ticket(s), showing 1");
        text.Should().Contain("11111111-2222-3333-4444-555555555555");
        text.Should().Contain("\"Cannot log in\"");
        text.Should().Contain("status=InProgress");
        text.Should().Contain("assignee=Alex");
        text.Should().Contain("priority=High");
    }

    [Fact]
    public void FormatResult_NullAssignee_ShowsUnassigned()
    {
        var dto = new TicketListItemDto(
            Id: TicketId.New(),
            Title: "Orphan",
            Status: TicketStatus.New,
            Priority: TicketPriority.Low,
            CreatedAt: DateTimeOffset.UtcNow,
            DueAt: null,
            Assignee: null,
            AllowedTransitions: [],
            IsOverdue: false,
            DisplayRef: "TCK-2");
        var result = new SearchTicketsResult(new PagedResult<TicketListItemDto>([dto], TotalCount: 1, Paging.Default));

        var text = SearchTicketsTool.FormatResult(result);

        text.Should().Contain("assignee=unassigned");
    }

    [Fact]
    public void FormatResult_NoItems_SaysSo()
    {
        var result = new SearchTicketsResult(new PagedResult<TicketListItemDto>([], TotalCount: 0, Paging.Default));

        var text = SearchTicketsTool.FormatResult(result);

        text.Should().Contain("No tickets matched");
    }
}
