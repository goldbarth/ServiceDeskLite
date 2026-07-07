using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Application.Tickets.Shared;

/// <summary>
/// The human-facing ticket reference ("#ABC123") is derived from the id: the last 6 hex
/// chars of the Guid, upper-cased. This centralises formatting and the normalisation used
/// to look a ticket up by that reference (case-insensitive, tolerates a leading '#').
/// </summary>
public static class TicketReference
{
    private const int Length = 6;

    /// <summary>The display reference for a ticket id, e.g. "#ABC123".</summary>
    public static string Format(TicketId id) => "#" + Suffix(id).ToUpperInvariant();

    /// <summary>The 6 hex chars behind '#', lower-cased — the value stored/compared for lookup.</summary>
    public static string Suffix(TicketId id) => $"{id.Value:N}"[^Length..].ToLowerInvariant();

    /// <summary>
    /// Normalises user/model input ("#ABC123", "abc123", " ABC123 ") to the lower-case
    /// suffix used for matching. Returns null when there is nothing usable to match on.
    /// </summary>
    public static string? Normalize(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return null;

        var cleaned = reference.Trim().TrimStart('#').Trim().ToLowerInvariant();
        if (cleaned.Length == 0)
            return null;

        return cleaned.Length > Length ? cleaned[^Length..] : cleaned;
    }
}
