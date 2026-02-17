using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

using System.Net;

using FluentAssertions;

using Serilog.Events;

using ServiceDeskLite.Tests.Api.Infrastructure;

namespace ServiceDeskLite.Tests.Api.ErrorHandling;

public sealed class UnhandledException_Logging_Tests
{
    [Fact]
    public async Task UnhandledException_Returns500_AndIsLogged()
    {
        var baseFactory = new ApiWebApplicationFactory();

        var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IStartupFilter>(new TestEndpointFilter(app =>
                {
                    app.Use(async (ctx, next) =>
                    {
                        if (ctx.Request.Path.StartsWithSegments("/_test/throw"))
                            throw new ApplicationException("boom");

                        await next(ctx);
                    });
                }));
            });
        });

        var client = factory.CreateClient();

        var response = await client.GetAsync("/_test/throw");
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        baseFactory.Sink.Events.Should()
            .Contain(e =>
                e.Level == LogEventLevel.Error &&
                e.RenderMessage()
                    .Contains("Unhandled exception."));
    }
}
