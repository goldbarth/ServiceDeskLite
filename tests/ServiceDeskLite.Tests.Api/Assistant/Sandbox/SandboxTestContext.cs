using System.Text.Json;

using Microsoft.Extensions.Options;

using ServiceDeskLite.Api.Assistant.Sandbox;
using ServiceDeskLite.Application.Abstractions.Assistant;
using ServiceDeskLite.Application.Common;

namespace ServiceDeskLite.Tests.Api.Assistant.Sandbox;

/// <summary>Shared fixtures for the guard tests.</summary>
internal static class SandboxTestContext
{
    public static readonly OwnerId Owner = new(Guid.Parse("00000000-0000-0000-0000-0000000000aa"));

    public static IOptions<AgentSandboxOptions> Options(AgentSandboxOptions options)
        => Microsoft.Extensions.Options.Options.Create(options);

    public static JsonElement Json(string raw) => JsonDocument.Parse(raw).RootElement;

    public static ToolInvocationContext Context(
        string toolName, string input = "{}", ToolTurnState? turn = null)
        => new(toolName, Json(input), Owner, turn ?? new ToolTurnState());
}

internal sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 7, 9, 12, 0, 0, TimeSpan.Zero);

    public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
}
