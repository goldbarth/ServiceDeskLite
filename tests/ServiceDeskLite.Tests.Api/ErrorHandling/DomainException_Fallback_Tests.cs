using System.Net;
using System.Net.Http.Json;

using FluentAssertions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

using ServiceDeskLite.Domain.Common;
using ServiceDeskLite.Tests.Api.Infrastructure;

namespace ServiceDeskLite.Tests.Api.ErrorHandling;

public class DomainException_Fallback_Tests
{
    [Fact]
    public async Task DomainException_Returns400_ProblemDetails()
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
                        if (ctx.Request.Path.StartsWithSegments("/_test/domain-ex"))
                        {
                            var error = new DomainError(
                                Code: "domain.ticket.status.invalid_transition",
                                Message: "Invalid transition.");
                            throw new DomainException(error);
                        }

                        await next(ctx);
                    });
                }));
            });
        });

        var client = factory.CreateClient();
        var response = await client.GetAsync("/_test/domain-ex");

        response.StatusCode.Should()
            .Be(HttpStatusCode.BadRequest);

        var pd = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        pd.Should()
            .NotBeNull();

        pd!.Status.Should().Be((int) HttpStatusCode.BadRequest);
        pd.Title.Should().NotBeNullOrWhiteSpace();

        pd.Extensions.Should().ContainKey("code");
        pd.Extensions["code"]!.ToString().Should().Be("domain.ticket.status.invalid_transition");

        pd.Extensions.Should().ContainKey("errorType");
        pd.Extensions.Should().ContainKey("traceId");

        pd.Detail.Should().BeNull();
    }
}
