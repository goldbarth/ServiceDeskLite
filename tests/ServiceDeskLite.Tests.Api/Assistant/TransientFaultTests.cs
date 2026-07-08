using System.Net;

using FluentAssertions;

using ServiceDeskLite.Api.Assistant;

namespace ServiceDeskLite.Tests.Api.Assistant;

public sealed class TransientFaultTests
{
    private static readonly CancellationToken NotCancelled = CancellationToken.None;

    [Theory]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public void Http_transient_status_codes_are_transient(HttpStatusCode status)
    {
        var ex = new HttpRequestException("boom", inner: null, statusCode: status);

        TransientFault.IsTransient(ex, NotCancelled).Should().BeTrue();
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public void Http_client_error_status_codes_are_not_transient(HttpStatusCode status)
    {
        var ex = new HttpRequestException("boom", inner: null, statusCode: status);

        TransientFault.IsTransient(ex, NotCancelled).Should().BeFalse();
    }

    [Fact]
    public void Timeout_is_transient()
    {
        TransientFault.IsTransient(new TimeoutException(), NotCancelled).Should().BeTrue();
    }

    [Fact]
    public void Cancellation_from_caller_is_not_transient()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        TransientFault.IsTransient(new OperationCanceledException(), cts.Token).Should().BeFalse();
    }

    [Fact]
    public void Cancellation_not_from_caller_is_transient()
    {
        // A request/operation timeout surfaces as a cancellation whose token is not the caller's.
        TransientFault.IsTransient(new OperationCanceledException(), NotCancelled).Should().BeTrue();
    }

    [Fact]
    public void Unknown_exception_is_not_transient()
    {
        TransientFault.IsTransient(new InvalidOperationException(), NotCancelled).Should().BeFalse();
    }
}
