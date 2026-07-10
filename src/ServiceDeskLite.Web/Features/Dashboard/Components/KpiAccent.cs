namespace ServiceDeskLite.Web.Features.Dashboard.Components;

/// <summary>
/// The tone a dashboard tile carries. It selects the tile's accent, so a call site names
/// what the number means rather than which colour it happens to use.
/// </summary>
public enum KpiAccent
{
    /// <summary>A plain count that carries no judgement.</summary>
    Neutral,

    /// <summary>Incoming or informational volume.</summary>
    Info,

    /// <summary>Something the system did on its own and wants to draw the eye to.</summary>
    Highlight,

    /// <summary>Work in flight, or a number worth watching before it turns bad.</summary>
    Caution,

    /// <summary>A number that needs someone to act.</summary>
    Critical,

    /// <summary>Work that finished well.</summary>
    Positive
}
