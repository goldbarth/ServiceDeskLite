using System.Text.Json.Serialization;

using ServiceDeskLite.Contracts.V1.Assistant;

namespace ServiceDeskLite.Api.Assistant;

/// <summary>
/// Payload of a single SSE event on the ticket-summary stream. The SSE event name
/// (delta, error, done) is carried by SseItem.EventType; unused fields are omitted
/// from the JSON. Every delta names its own section, so the client never has to
/// infer the current section from stream order.
/// </summary>
public sealed record TicketSummarySseEvent(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] TicketSummarySection? Section = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Text = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Message = null)
{
    /// <summary>A chunk of text for one section; carries <c>Section</c> and <c>Text</c>.</summary>
    public const string DeltaEvent = "delta";
    public const string ErrorEvent = "error";
    public const string DoneEvent = "done";
}
