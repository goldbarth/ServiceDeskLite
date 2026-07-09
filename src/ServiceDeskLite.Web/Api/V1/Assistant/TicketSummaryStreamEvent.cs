using ServiceDeskLite.Contracts.V1.Assistant;

namespace ServiceDeskLite.Web.Api.V1.Assistant;

/// <summary>
/// One event from the ticket-summary SSE stream. EventType mirrors the server-side
/// event names: delta, error, done. A delta always names the section it belongs to.
/// </summary>
public sealed record TicketSummaryStreamEvent(
    string EventType,
    TicketSummarySection? Section = null,
    string? Text = null,
    string? Message = null)
{
    public const string DeltaEvent = "delta";
    public const string ErrorEvent = "error";
    public const string DoneEvent = "done";
}
