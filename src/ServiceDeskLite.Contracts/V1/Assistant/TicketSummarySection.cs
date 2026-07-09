using System.Text.Json.Serialization;

namespace ServiceDeskLite.Contracts.V1.Assistant;

/// <summary>
/// The four parts of a streamed ticket summary. Every delta on the summary
/// stream is tagged with the section it belongs to, so the client can route it
/// to the right panel without tracking stream order.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<TicketSummarySection>))]
public enum TicketSummarySection
{
    Summary,
    NextSteps,
    Risks,
    MissingInfo,
}
