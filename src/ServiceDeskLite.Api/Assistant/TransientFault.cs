using System.Net;

namespace ServiceDeskLite.Api.Assistant;

/// <summary>
/// Classifies whether a tool failure is worth retrying. Deliberately conservative:
/// only well-known transient conditions (rate limits, upstream 5xx, timeouts) count;
/// anything unrecognized is treated as permanent so it surfaces to the model once
/// instead of being retried pointlessly.
/// </summary>
public static class TransientFault
{
    private static readonly HttpStatusCode[] TransientStatusCodes =
    [
        HttpStatusCode.RequestTimeout,        // 408
        HttpStatusCode.TooManyRequests,       // 429
        HttpStatusCode.InternalServerError,   // 500
        HttpStatusCode.BadGateway,            // 502
        HttpStatusCode.ServiceUnavailable,    // 503
        HttpStatusCode.GatewayTimeout,        // 504
    ];

    /// <param name="callerToken">
    /// The request's cancellation token: a cancellation tied to it is the client
    /// disconnecting (not transient); a cancellation from anywhere else is an
    /// operation timeout (transient).
    /// </param>
    public static bool IsTransient(Exception ex, CancellationToken callerToken)
    {
        switch (ex)
        {
            case HttpRequestException { StatusCode: { } status }:
                return Array.IndexOf(TransientStatusCodes, status) >= 0;

            case TimeoutException:
                return true;

            // A timeout surfaces as a cancellation whose token is not the caller's.
            case OperationCanceledException:
                return !callerToken.IsCancellationRequested;

            default:
                return false;
        }
    }
}
