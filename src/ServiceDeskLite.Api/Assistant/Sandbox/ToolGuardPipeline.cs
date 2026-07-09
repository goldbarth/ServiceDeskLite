namespace ServiceDeskLite.Api.Assistant.Sandbox;

/// <summary>
/// Runs every <see cref="IToolGuard"/> in front of a tool call. A refusal by any guard stops the
/// call before it reaches a command handler, and nothing is spent.
/// </summary>
/// <remarks>
/// The pipeline sits at the single point where the model's intent becomes execution, so a tool
/// cannot be added past it. Guards are order-independent by construction: each one only reads
/// state in <see cref="IToolGuard.Check"/>, and no guard spends anything until all of them have
/// admitted the call.
/// </remarks>
public sealed class ToolGuardPipeline
{
    private readonly IReadOnlyList<IToolGuard> _guards;
    private readonly ILogger<ToolGuardPipeline> _logger;

    public ToolGuardPipeline(IEnumerable<IToolGuard> guards, ILogger<ToolGuardPipeline> logger)
    {
        _guards = guards?.ToArray() ?? throw new ArgumentNullException(nameof(guards));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <returns>An allowing result, or the first refusal with the reason the model will be told.</returns>
    public ToolGuardResult Admit(ToolInvocationContext context)
    {
        foreach (var guard in _guards)
        {
            var result = guard.Check(context);
            if (result.IsAllowed)
                continue;

            _logger.LogWarning(
                "Tool {Tool} refused by {Guard}: {Reason}",
                context.ToolName, guard.GetType().Name, result.Reason);

            return result;
        }

        foreach (var guard in _guards)
            guard.Commit(context);

        return ToolGuardResult.Allow();
    }
}
