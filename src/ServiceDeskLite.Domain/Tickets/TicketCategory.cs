namespace ServiceDeskLite.Domain.Tickets;

/// <summary>
/// Coarse triage category of a ticket, derived from its content by auto-routing.
/// <see cref="Uncategorized"/> (the default) means "not yet routed"; a router assigns
/// one of the concrete categories (falling back to <see cref="Other"/> when nothing
/// matches), distinguishing an unclassified ticket from one deliberately routed to Other.
/// </summary>
public enum TicketCategory
{
    Uncategorized,
    Network,
    Hardware,
    Software,
    Access,
    Account,
    Other,
}
