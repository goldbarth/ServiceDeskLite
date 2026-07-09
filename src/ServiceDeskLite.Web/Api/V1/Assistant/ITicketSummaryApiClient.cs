namespace ServiceDeskLite.Web.Api.V1.Assistant;

public interface ITicketSummaryApiClient
{
    /// <summary>
    /// Streams the AI summary of one ticket. Each yielded delta carries the section it
    /// belongs to, so sections may be rendered as they arrive. The stream ends with a
    /// <c>done</c> or <c>error</c> event.
    /// </summary>
    IAsyncEnumerable<TicketSummaryStreamEvent> StreamAsync(Guid ticketId, CancellationToken ct = default);
}
