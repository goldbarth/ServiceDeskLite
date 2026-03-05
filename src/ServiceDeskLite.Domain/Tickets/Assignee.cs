using ServiceDeskLite.Domain.Common;

namespace ServiceDeskLite.Domain.Tickets;

public readonly record struct Assignee
{
    public const int MaxNameLength = 100;

    public string Name { get; }

    public Assignee(string name)
    {
        Guard.NotNullOrWhiteSpace(name, nameof(name));
        Guard.MaxLength(name, MaxNameLength, nameof(name));
        Name = name;
    }
}