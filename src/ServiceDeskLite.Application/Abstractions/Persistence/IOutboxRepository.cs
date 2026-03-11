using ServiceDeskLite.Domain.Outbox;

namespace ServiceDeskLite.Application.Abstractions.Persistence;

/// <summary>
/// Port for staging outbox messages within the current unit-of-work boundary.
///
/// SHOWCASE STUB — a production implementation would also expose methods for
/// the background dispatcher: e.g. <c>GetPendingAsync</c> and batch-update
/// of dispatched messages. Those are intentionally omitted here.
/// See ADR 0021.
/// </summary>
public interface IOutboxRepository
{
    /// <summary>
    /// Stages an outbox message for persistence.
    /// The message is not durable until <see cref="IUnitOfWork.SaveChangesAsync"/> is called.
    /// </summary>
    Task AddAsync(OutboxMessage message, CancellationToken ct = default);
}