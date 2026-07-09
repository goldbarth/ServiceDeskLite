using ServiceDeskLite.Application.Abstractions.Assistant;

namespace ServiceDeskLite.Api.Assistant.Sandbox;

/// <summary>
/// The set of tools the model may call, and what each one is for.
/// One list, three readers: the sandbox rejects a name that is not here, the dashboard
/// aggregates on the kind, and <see cref="AssistantChatService"/> dispatches on the name.
/// A tool wired into the dispatch but forgotten here is refused rather than run unguarded.
/// </summary>
public static class ToolCatalog
{
    public static readonly IReadOnlyDictionary<string, AssistantToolKind> Kinds =
        new Dictionary<string, AssistantToolKind>(StringComparer.Ordinal)
        {
            [CreateTicketTool.Name] = AssistantToolKind.Action,
            [UpdateTicketTool.Name] = AssistantToolKind.Action,
            [ChangeTicketStatusTool.Name] = AssistantToolKind.Action,
            [AssignTicketTool.Name] = AssistantToolKind.Action,
            [RouteTicketTool.Name] = AssistantToolKind.Action,
            [RememberTool.Name] = AssistantToolKind.Action,
            [FindSimilarTicketsTool.Name] = AssistantToolKind.DuplicateCheck,
            [SearchTicketsTool.Name] = AssistantToolKind.Retrieval,
            [SearchKnowledgeBaseTool.Name] = AssistantToolKind.Retrieval,
            [RecallMemoryTool.Name] = AssistantToolKind.Retrieval,
            [CheckGroundingTool.Name] = AssistantToolKind.Evaluation,
        };

    public static bool IsKnown(string toolName) => Kinds.ContainsKey(toolName);

    public static AssistantToolKind KindOf(string toolName) =>
        Kinds.TryGetValue(toolName, out var kind) ? kind : AssistantToolKind.Action;

    /// <summary>
    /// Tools that reach a command handler and change a ticket or a memory. The write budget
    /// counts these; a retrieval that runs away wastes tokens, a write that runs away
    /// changes the workspace.
    /// </summary>
    public static bool IsWrite(string toolName) => KindOf(toolName) == AssistantToolKind.Action;
}
