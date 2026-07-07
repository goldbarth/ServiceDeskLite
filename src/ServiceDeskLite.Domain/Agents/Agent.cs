using ServiceDeskLite.Domain.Common;

namespace ServiceDeskLite.Domain.Agents;

/// <summary>
/// A service-desk agent who can be assigned tickets. Fictitious accounts seeded for
/// the demo — the roster of who may be assigned. Tickets reference an agent by id
/// (FK); the agent's name is resolved for display and snapshotted into the audit trail
/// at assignment time (ADR-0025).
/// </summary>
public sealed class Agent
{
    public const int MaxNameLength = 100;
    public const int MaxEmailLength = 200;

    public AgentId Id { get; }
    public string Name { get; private set; }
    public string Email { get; private set; }
    public bool Active { get; private set; }

    // Private constructor for EF Core materialization.
#pragma warning disable CS8618
    private Agent() { }
#pragma warning restore CS8618

    public Agent(AgentId id, string name, string email, bool active = true)
    {
        Guard.NotNullOrWhiteSpace(name, nameof(name));
        Guard.MaxLength(name, MaxNameLength, nameof(name));
        Guard.NotNullOrWhiteSpace(email, nameof(email));
        Guard.MaxLength(email, MaxEmailLength, nameof(email));

        Id = id;
        Name = name;
        Email = email;
        Active = active;
    }
}
