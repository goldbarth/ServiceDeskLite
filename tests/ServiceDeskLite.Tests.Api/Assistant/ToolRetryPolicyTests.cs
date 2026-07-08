using System.Net;

using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using ServiceDeskLite.Api.Assistant;

namespace ServiceDeskLite.Tests.Api.Assistant;

public sealed class ToolRetryPolicyTests
{
    private static readonly Func<int, TimeSpan> NoDelay = _ => TimeSpan.Zero;

    private static HttpRequestException Transient() =>
        new("rate limited", inner: null, statusCode: HttpStatusCode.TooManyRequests);

    private static Task<(string, bool, Guid?, double?)> Ok() =>
        Task.FromResult(("done", false, (Guid?)null, (double?)null));

    [Fact]
    public async Task Retries_transient_failure_then_returns_success()
    {
        var attempts = 0;

        var (content, isError, _, _) = await ToolRetryPolicy.ExecuteAsync(
            _ =>
            {
                attempts++;
                return attempts <= 2 ? throw Transient() : Ok();
            },
            maxRetries: 2, NoDelay, NullLogger.Instance, CancellationToken.None);

        attempts.Should().Be(3);
        isError.Should().BeFalse();
        content.Should().Be("done");
    }

    [Fact]
    public async Task Exhausted_transient_failure_surfaces_as_error()
    {
        var attempts = 0;

        var (_, isError, _, _) = await ToolRetryPolicy.ExecuteAsync(
            _ =>
            {
                attempts++;
                throw Transient();
            },
            maxRetries: 2, NoDelay, NullLogger.Instance, CancellationToken.None);

        attempts.Should().Be(3, "the first try plus two retries");
        isError.Should().BeTrue();
    }

    [Fact]
    public async Task Non_transient_exception_is_not_retried()
    {
        var attempts = 0;

        var (_, isError, _, _) = await ToolRetryPolicy.ExecuteAsync(
            _ =>
            {
                attempts++;
                throw new InvalidOperationException();
            },
            maxRetries: 3, NoDelay, NullLogger.Instance, CancellationToken.None);

        attempts.Should().Be(1);
        isError.Should().BeTrue();
    }

    [Fact]
    public async Task Deterministic_error_result_is_returned_without_retry()
    {
        var attempts = 0;

        var (content, isError, _, _) = await ToolRetryPolicy.ExecuteAsync(
            _ =>
            {
                attempts++;
                return Task.FromResult(("invalid input", true, (Guid?)null, (double?)null));
            },
            maxRetries: 3, NoDelay, NullLogger.Instance, CancellationToken.None);

        attempts.Should().Be(1, "a returned is_error is deterministic — the model self-corrects, no retry");
        isError.Should().BeTrue();
        content.Should().Be("invalid input");
    }

    [Fact]
    public async Task Caller_cancellation_propagates()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await ToolRetryPolicy.ExecuteAsync(
            (Func<CancellationToken, Task<(string, bool, Guid?, double?)>>)(_ => throw new OperationCanceledException(cts.Token)),
            maxRetries: 2, NoDelay, NullLogger.Instance, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
