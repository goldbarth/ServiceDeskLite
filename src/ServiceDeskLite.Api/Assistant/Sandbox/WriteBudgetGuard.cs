using Microsoft.Extensions.Options;

namespace ServiceDeskLite.Api.Assistant.Sandbox;

/// <summary>
/// Bounds how many writes one chat turn may perform.
/// <c>Anthropic:MaxToolIterations</c> already bounds the number of model round trips, but the
/// model may request several tools per round, so a turn's write count is not bounded by it.
/// A retrieval loop wastes tokens; a write loop changes the workspace, and only this stops it.
/// </summary>
public sealed class WriteBudgetGuard : IToolGuard
{
    private readonly AgentSandboxOptions _options;

    public WriteBudgetGuard(IOptions<AgentSandboxOptions> options)
        => _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    public ToolGuardResult Check(ToolInvocationContext context)
    {
        if (!ToolCatalog.IsWrite(context.ToolName))
            return ToolGuardResult.Allow();

        if (context.Turn.Writes < _options.MaxWritesPerTurn)
            return ToolGuardResult.Allow();

        var change = _options.MaxWritesPerTurn == 1 ? "change" : "changes";
        return ToolGuardResult.Deny(
            $"This turn has already made {_options.MaxWritesPerTurn} {change}, which is the limit. "
            + "Stop calling tools that modify tickets and summarize what you did.");
    }

    public void Commit(ToolInvocationContext context)
    {
        if (ToolCatalog.IsWrite(context.ToolName))
            context.Turn.CountWrite();
    }
}
