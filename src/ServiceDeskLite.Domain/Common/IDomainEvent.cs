namespace ServiceDeskLite.Domain.Common;

/// <summary>
/// Marker interface for domain events raised by aggregates.
/// Domain events capture what happened inside the domain as a result of
/// a state-changing operation. They are recorded on the aggregate and
/// processed by the application layer after the operation completes.
/// </summary>
public interface IDomainEvent { }
