using FluentAssertions;

using ServiceDeskLite.Domain.Agents;
using ServiceDeskLite.Domain.Common;

namespace ServiceDeskLite.Tests.Domain.Agents;

public sealed class AgentTests
{
    [Fact]
    public void Constructor_ValidValues_SetsProperties()
    {
        var id = AgentId.New();

        var agent = new Agent(id, "Alex Kim", "alex.kim@servicedesk.example");

        agent.Id.Should().Be(id);
        agent.Name.Should().Be("Alex Kim");
        agent.Email.Should().Be("alex.kim@servicedesk.example");
        agent.Active.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_BlankName_Throws(string name)
    {
        var act = () => new Agent(AgentId.New(), name, "a@b.example");

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Constructor_NameTooLong_Throws()
    {
        var name = new string('x', Agent.MaxNameLength + 1);

        var act = () => new Agent(AgentId.New(), name, "a@b.example");

        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_BlankEmail_Throws(string email)
    {
        var act = () => new Agent(AgentId.New(), "Alex Kim", email);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Constructor_InactiveFlag_IsHonoured()
    {
        var agent = new Agent(AgentId.New(), "Sam Rivera", "sam@servicedesk.example", active: false);

        agent.Active.Should().BeFalse();
    }
}
