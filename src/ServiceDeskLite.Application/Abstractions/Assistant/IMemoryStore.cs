namespace ServiceDeskLite.Application.Abstractions.Assistant;

/// <summary>
/// Writes long-term memories (durable facts, preferences, profile notes) for an
/// owner. Embedding-backed, so it needs the Voyage embedding provider on Postgres;
/// other deployments report <see cref="MemoryWriteResult.IsAvailable"/> = false so
/// the assistant can tell the model honestly instead of pretending it stored a fact.
/// </summary>
public interface IMemoryStore
{
    Task<MemoryWriteResult> AddAsync(OwnerId owner, string content, string kind, CancellationToken ct);
}

public sealed record MemoryWriteResult(bool IsAvailable)
{
    public static MemoryWriteResult Unavailable { get; } = new(false);
    public static MemoryWriteResult Stored { get; } = new(true);
}
