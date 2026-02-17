using System.Net;
using System.Net.Http.Json;

using FluentAssertions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

using ServiceDeskLite.Tests.Api.Infrastructure;

namespace ServiceDeskLite.Tests.Api.ErrorHandling;

public class BadRequest_Binding_Tests
{
    [Fact]
    public async Task BadRequest_Returns400_ProblemDetails()
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
                        if (ctx.Request.Path.StartsWithSegments("/_test/bad-request"))
                            throw new BadHttpRequestException("Invalid request payload.");

                        await next(ctx);
                    });
                }));
            });
        });

        var client = factory.CreateClient();
        var response = await client.GetAsync("/_test/bad-request");

        response.StatusCode.Should()
            .Be(HttpStatusCode.BadRequest);

        var pd = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        pd.Should().NotBeNull();

        pd!.Extensions.Should().ContainKey("code");
        pd.Extensions.Should().ContainKey("errorType");
        pd.Extensions.Should().ContainKey("traceId");
        pd.Detail.Should().BeNull();
    }
}
