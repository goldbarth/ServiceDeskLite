using FluentAssertions;

using ServiceDeskLite.Contracts.V1.Common;
using ServiceDeskLite.Contracts.V1.Tickets;
using ServiceDeskLite.Web.Features.Tickets.State;

namespace ServiceDeskLite.Tests.Web.Features.Tickets.State;

public class TicketQueryParamsTests
{
    // ── Default ────────────────────────────────────────────────────────────

    [Fact]
    public void Default_HasExpectedValues()
    {
        var d = TicketQueryParams.Default;

        d.Page.Should().Be(1);
        d.PageSize.Should().Be(25);
        d.SortField.Should().Be(TicketSortField.CreatedAt);
        d.SortDirection.Should().Be(SortDirection.Desc);
    }

    // ── WithSort ────────────────────────────────────────────────────────────

    [Fact]
    public void WithSort_DifferentField_SetsAscAndResetsPageToOne()
    {
        var original = TicketQueryParams.Default with { Page = 3, SortField = TicketSortField.Status };

        var result = original.WithSort(TicketSortField.Priority);

        result.SortField.Should().Be(TicketSortField.Priority);
        result.SortDirection.Should().Be(SortDirection.Asc);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(original.PageSize);
    }

    [Fact]
    public void WithSort_SameField_Desc_TogglesTo_Asc()
    {
        var original = TicketQueryParams.Default with
        {
            SortField = TicketSortField.Priority,
            SortDirection = SortDirection.Desc
        };

        var result = original.WithSort(TicketSortField.Priority);

        result.SortDirection.Should().Be(SortDirection.Asc);
        result.SortField.Should().Be(TicketSortField.Priority);
        result.Page.Should().Be(1);
    }

    [Fact]
    public void WithSort_SameField_Asc_TogglesTo_Desc()
    {
        var original = TicketQueryParams.Default with
        {
            SortField = TicketSortField.Title,
            SortDirection = SortDirection.Asc
        };

        var result = original.WithSort(TicketSortField.Title);

        result.SortDirection.Should().Be(SortDirection.Desc);
    }

    // ── ToSearchRequest ─────────────────────────────────────────────────────

    [Fact]
    public void ToSearchRequest_MapsAllFields()
    {
        var query = new TicketQueryParams(
            Page: 3,
            PageSize: 50,
            SortField: TicketSortField.DueAt,
            SortDirection: SortDirection.Asc);

        var req = query.ToSearchRequest();

        req.Page.Should().Be(3);
        req.PageSize.Should().Be(50);
        req.SortField.Should().Be(TicketSortField.DueAt);
        req.SortDirection.Should().Be(SortDirection.Asc);
    }

    // ── Value equality (record semantics) ───────────────────────────────────

    [Fact]
    public void TwoInstancesWithSameValues_AreEqual()
    {
        var a = new TicketQueryParams(1, 25, TicketSortField.CreatedAt, SortDirection.Desc);
        var b = new TicketQueryParams(1, 25, TicketSortField.CreatedAt, SortDirection.Desc);

        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void WithExpression_ProducesNewInstanceWithChangedField()
    {
        var original = TicketQueryParams.Default;
        var modified = original with { Page = 5 };

        modified.Page.Should().Be(5);
        modified.Should().NotBe(original);
        original.Page.Should().Be(1); // original unchanged
    }
}
