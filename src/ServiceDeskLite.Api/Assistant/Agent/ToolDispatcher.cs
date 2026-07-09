using System.Text.Json;

using Anthropic.Models.Messages;

using ServiceDeskLite.Application.Common;

namespace ServiceDeskLite.Api.Assistant.Agent;

/// <summary>
/// The tools the model may call, and the one place a tool name becomes an execution.
/// </summary>
/// <remarks>
/// Split out of <see cref="AssistantChatService"/> so <see cref="AgentLoop"/> knows nothing about
/// any particular tool: it admits a call, dispatches it here, and reports the result. Both the
/// chat assistant and the autonomous worker run the same tools through the same dispatch, so a
/// tool cannot behave one way for a user and another way for the worker.
/// </remarks>
public sealed class ToolDispatcher
{
    private readonly CreateTicketTool _createTool;
    private readonly UpdateTicketTool _updateTool;
    private readonly FindSimilarTicketsTool _findSimilarTool;
    private readonly SearchTicketsTool _searchTool;
    private readonly ChangeTicketStatusTool _changeStatusTool;
    private readonly AssignTicketTool _assignTool;
    private readonly RouteTicketTool _routeTool;
    private readonly SearchKnowledgeBaseTool _knowledgeTool;
    private readonly CheckGroundingTool _groundingTool;
    private readonly RememberTool _rememberTool;
    private readonly RecallMemoryTool _recallTool;
    private readonly AddCommentTool _commentTool;
    private readonly IClock _clock;

    public ToolDispatcher(
        CreateTicketTool createTool,
        UpdateTicketTool updateTool,
        FindSimilarTicketsTool findSimilarTool,
        SearchTicketsTool searchTool,
        ChangeTicketStatusTool changeStatusTool,
        AssignTicketTool assignTool,
        RouteTicketTool routeTool,
        SearchKnowledgeBaseTool knowledgeTool,
        CheckGroundingTool groundingTool,
        RememberTool rememberTool,
        RecallMemoryTool recallTool,
        AddCommentTool commentTool,
        IClock clock)
    {
        _createTool = createTool ?? throw new ArgumentNullException(nameof(createTool));
        _updateTool = updateTool ?? throw new ArgumentNullException(nameof(updateTool));
        _findSimilarTool = findSimilarTool ?? throw new ArgumentNullException(nameof(findSimilarTool));
        _searchTool = searchTool ?? throw new ArgumentNullException(nameof(searchTool));
        _changeStatusTool = changeStatusTool ?? throw new ArgumentNullException(nameof(changeStatusTool));
        _assignTool = assignTool ?? throw new ArgumentNullException(nameof(assignTool));
        _routeTool = routeTool ?? throw new ArgumentNullException(nameof(routeTool));
        _knowledgeTool = knowledgeTool ?? throw new ArgumentNullException(nameof(knowledgeTool));
        _groundingTool = groundingTool ?? throw new ArgumentNullException(nameof(groundingTool));
        _rememberTool = rememberTool ?? throw new ArgumentNullException(nameof(rememberTool));
        _recallTool = recallTool ?? throw new ArgumentNullException(nameof(recallTool));
        _commentTool = commentTool ?? throw new ArgumentNullException(nameof(commentTool));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>
    /// Every tool definition sent to the model. One list for both agents: the worker is bounded by
    /// what the sandbox admits (ADR-0035) and by its prompt, not by hiding tools from it.
    /// </summary>
    public static IReadOnlyList<Tool> Definitions =>
    [
        CreateTicketTool.Definition,
        UpdateTicketTool.Definition,
        FindSimilarTicketsTool.Definition,
        SearchTicketsTool.Definition,
        ChangeTicketStatusTool.Definition,
        AssignTicketTool.Definition,
        RouteTicketTool.Definition,
        SearchKnowledgeBaseTool.Definition,
        CheckGroundingTool.Definition,
        RememberTool.Definition,
        RecallMemoryTool.Definition,
        AddCommentTool.Definition,
    ];

    public Task<ToolResult> ExecuteAsync(string toolName, JsonElement input, CancellationToken ct) =>
        toolName switch
        {
            CreateTicketTool.Name => _createTool.ExecuteAsync(input, ct),
            UpdateTicketTool.Name => _updateTool.ExecuteAsync(input, _clock.UtcNow, ct),
            FindSimilarTicketsTool.Name => _findSimilarTool.ExecuteAsync(input, ct),
            SearchTicketsTool.Name => _searchTool.ExecuteAsync(input, ct),
            ChangeTicketStatusTool.Name => _changeStatusTool.ExecuteAsync(input, ct),
            AssignTicketTool.Name => _assignTool.ExecuteAsync(input, ct),
            RouteTicketTool.Name => _routeTool.ExecuteAsync(input, ct),
            SearchKnowledgeBaseTool.Name => _knowledgeTool.ExecuteAsync(input, ct),
            CheckGroundingTool.Name => _groundingTool.ExecuteAsync(input, ct),
            RememberTool.Name => _rememberTool.ExecuteAsync(input, ct),
            RecallMemoryTool.Name => _recallTool.ExecuteAsync(input, ct),
            AddCommentTool.Name => _commentTool.ExecuteAsync(input, ct),
            _ => Task.FromResult(new ToolResult($"Unknown tool '{toolName}'.", true)),
        };
}
