namespace ServiceDeskLite.Api.Assistant;

public sealed partial class SearchKnowledgeBaseTool
{
    private const string ToolDescription =
        """
        Search the internal knowledge base (help articles, FAQ, and internal docs) for
        guidance on how to resolve an issue. Use this when the user asks how to fix or do
        something, or when suggesting a solution, so the answer is grounded in documented
        procedure rather than guesswork. The query is matched by meaning, so describe the
        problem in a full sentence. Ground your answer in the returned passages and cite
        them by title. If the tool reports the knowledge base is unavailable or finds
        nothing, say so plainly — never invent a source.
        """;

    private const string QueryDescription =
        "The question or problem to look up, e.g. \"how do I reset a locked account?\".";

    private static readonly string LimitDescription =
        $"Maximum number of passages to return (default {DefaultLimit}).";
}
