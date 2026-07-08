namespace ServiceDeskLite.Api.Assistant;

public sealed partial class CheckGroundingTool
{
    private const string ToolDescription =
        """
        Verify that a drafted answer is actually supported by the knowledge-base passages
        you retrieved this turn, before you send it. Call this after search_knowledge_base
        whenever your answer relies on internal sources: pass your draft answer and it
        returns a grounding score and lists any statements the sources do not support. If
        the score is weak, revise — search again for support, drop the unsupported claims,
        or hedge them — instead of asserting them as documented fact. Checking the draft is
        internal; the user does not see this step.
        """;

    private const string AnswerDescription =
        "Your drafted answer to check against the retrieved knowledge-base passages.";
}
