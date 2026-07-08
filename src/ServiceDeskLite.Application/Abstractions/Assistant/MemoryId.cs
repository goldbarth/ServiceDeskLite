namespace ServiceDeskLite.Application.Abstractions.Assistant;

public readonly record struct MemoryId(Guid Value)
{
    public static MemoryId New() => new(Guid.CreateVersion7());
}
