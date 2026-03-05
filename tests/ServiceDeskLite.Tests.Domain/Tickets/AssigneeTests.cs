using FluentAssertions;

using ServiceDeskLite.Domain.Common;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Tests.Domain.Tickets;

public sealed class AssigneeTests
{
    // -----------------------------------------------------------------------
    // Valid construction
    // -----------------------------------------------------------------------

    [Fact]
    public void Ctor_ValidName_SetsName()
    {
        var assignee = new Assignee("Alice");

        assignee.Name.Should().Be("Alice");
    }

    [Fact]
    public void Ctor_NameAtMaxLength_Succeeds()
    {
        var name = new string('x', Assignee.MaxNameLength);

        var act = () => new Assignee(name);

        act.Should().NotThrow();
    }

    // -----------------------------------------------------------------------
    // Invariant violations
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Ctor_EmptyOrWhitespaceName_ThrowsDomainException(string name)
    {
        var act = () => new Assignee(name);

        act.Should().Throw<DomainException>()
            .Which.Error.Code.Should().Be("domain.not_empty");
    }

    [Fact]
    public void Ctor_NameExceedsMaxLength_ThrowsDomainException()
    {
        var tooLong = new string('x', Assignee.MaxNameLength + 1);

        var act = () => new Assignee(tooLong);

        act.Should().Throw<DomainException>()
            .Which.Error.Code.Should().Be("domain.max_length");
    }

    // -----------------------------------------------------------------------
    // Value Object semantics – structural equality (record struct)
    // -----------------------------------------------------------------------

    [Fact]
    public void TwoAssignees_WithSameName_AreEqual()
    {
        var a = new Assignee("Alice");
        var b = new Assignee("Alice");

        a.Should().Be(b);
    }

    [Fact]
    public void TwoAssignees_WithDifferentNames_AreNotEqual()
    {
        var a = new Assignee("Alice");
        var b = new Assignee("Bob");

        a.Should().NotBe(b);
    }
}