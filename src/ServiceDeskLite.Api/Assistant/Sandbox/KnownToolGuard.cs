namespace ServiceDeskLite.Api.Assistant.Sandbox;

/// <summary>
/// Refuses a tool name that is not in <see cref="ToolCatalog"/>.
/// The model cannot normally invent one, since the tool list is sent with every request, but a
/// name that reaches the dispatch without a catalog entry has no declared kind and therefore no
/// write classification — it would slip past the write budget. Refusing it is the honest answer.
/// </summary>
public sealed class KnownToolGuard : IToolGuard
{
    public ToolGuardResult Check(ToolInvocationContext context) =>
        ToolCatalog.IsKnown(context.ToolName)
            ? ToolGuardResult.Allow()
            : ToolGuardResult.Deny($"Unknown tool '{context.ToolName}'. Use only the tools you were given.");
}
