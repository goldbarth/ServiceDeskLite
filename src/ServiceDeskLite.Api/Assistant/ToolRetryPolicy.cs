namespace ServiceDeskLite.Api.Assistant;

/// <summary>
/// Retries a tool invocation on transient failures before giving up. Deterministic
/// failures (validation, workflow conflicts) are returned by the tool as
/// <c>is_error</c> results and are never retried here — the model self-corrects on
/// those. Only transient exceptions (see <see cref="TransientFault"/>) are retried,
/// with bounded exponential backoff; on exhaustion — or any non-transient exception —
/// the failure is turned into an <c>is_error</c> tool result so the model always gets
/// a response and the stream never breaks. Extracted from the streaming loop so it can
/// be unit-tested without the Anthropic client.
/// </summary>
public static class ToolRetryPolicy
{
    public static async Task<(string Content, bool IsError, Guid? TicketId, double? Confidence)> ExecuteAsync(
        Func<CancellationToken, Task<(string Content, bool IsError, Guid? TicketId, double? Confidence)>> action,
        int maxRetries,
        Func<int, TimeSpan> backoff,
        ILogger logger,
        CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await action(ct);
            }
            catch (Exception ex) when (attempt < maxRetries && TransientFault.IsTransient(ex, ct))
            {
                var delay = backoff(attempt);
                logger.LogWarning(ex,
                    "Transient tool failure on attempt {Attempt}/{Total}; retrying in {Delay}.",
                    attempt + 1, maxRetries + 1, delay);
                await Task.Delay(delay, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Client disconnected — abandon, do not swallow into an error result.
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Tool failed after {Attempts} attempt(s); surfacing as an error tool result.",
                    attempt + 1);
                return ("The tool failed due to a technical error. Continue without it and do not retry.",
                    true, null, null);
            }
        }
    }
}
