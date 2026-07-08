namespace ServiceDeskLite.Application.Abstractions.Assistant;

/// <summary>
/// Owner of a conversation or memory record. There is no real authentication
/// yet (the API key is a demo guard, not an identity), so today a single demo
/// owner is used everywhere — see <see cref="ICurrentUser"/>. Modelled as an id
/// from the start so real auth can later supply a per-user value without a
/// schema or contract change.
/// </summary>
public readonly record struct OwnerId(Guid Value);
