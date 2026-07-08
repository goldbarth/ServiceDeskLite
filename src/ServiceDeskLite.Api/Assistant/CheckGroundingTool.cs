using System.Globalization;
using System.Text;
using System.Text.Json;

using Anthropic.Models.Messages;

namespace ServiceDeskLite.Api.Assistant;

/// <summary>
/// RAG self-evaluation exposed as a tool (issue #158): the model passes its drafted
/// answer, and this scores it against the knowledge-base passages actually retrieved
/// this turn (held in <see cref="IRagRetrievalContext"/>) using the deterministic
/// <see cref="GroundingEvaluator"/>. A weak/ungrounded result comes back with the
/// unsupported sentences and an instruction to re-retrieve or hedge, so the model
/// corrects itself before asserting. Checking the draft (not the streamed answer)
/// keeps token streaming intact — the verified answer is still emitted only once.
/// </summary>
public sealed partial class CheckGroundingTool
{
    public const string Name = "check_grounding";

    private readonly IRagRetrievalContext _retrieval;

    public CheckGroundingTool(IRagRetrievalContext retrieval)
    {
        _retrieval = retrieval ?? throw new ArgumentNullException(nameof(retrieval));
    }

    public static Tool Definition => new()
    {
        Name = Name,
        Description = ToolDescription,
        InputSchema = new()
        {
            Properties = new Dictionary<string, JsonElement>
            {
                ["answer"] = JsonSerializer.SerializeToElement(new
                {
                    type = "string",
                    description = AnswerDescription,
                }),
            },
            Required = ["answer"],
        },
    };

    public static bool TryParseInput(JsonElement input, out string answer, out string? error)
    {
        answer = string.Empty;

        if (input.ValueKind is not JsonValueKind.Object)
        {
            error = "Tool input must be a JSON object.";
            return false;
        }

        if (!input.TryGetProperty("answer", out var answerEl)
            || answerEl.ValueKind is not JsonValueKind.String
            || string.IsNullOrWhiteSpace(answerEl.GetString()))
        {
            error = "Missing or invalid required string property 'answer'.";
            return false;
        }

        answer = answerEl.GetString()!;
        error = null;
        return true;
    }

    /// <summary>Formats the grounding report for the model, steering correction when support is weak.</summary>
    public static string FormatReport(GroundingReport report, int sourceCount)
    {
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture,
            $"Grounding score {report.Score:P0} ({report.Verdict}) against {sourceCount} retrieved passage(s).");

        if (report.Verdict == GroundingVerdict.Grounded)
        {
            sb.Append(" The draft is well supported; you may give it as your answer.");
            return sb.ToString();
        }

        if (report.Unsupported.Count > 0)
        {
            sb.AppendLine();
            sb.Append("Unsupported statements:");
            foreach (var claim in report.Unsupported)
            {
                sb.AppendLine();
                sb.Append(CultureInfo.InvariantCulture, $"- \"{claim}\"");
            }
        }

        sb.AppendLine();
        sb.Append(
            "Do not assert these as sourced fact. Either search the knowledge base again for support, "
            + "drop the unsupported claims, or hedge them explicitly (say the knowledge base does not confirm them).");

        return sb.ToString();
    }

    public Task<ToolResult> ExecuteAsync(JsonElement input, CancellationToken ct)
    {
        if (!TryParseInput(input, out var answer, out var parseError))
            return Task.FromResult(new ToolResult($"Invalid tool input: {parseError}", true));

        var passages = _retrieval.Passages;
        if (passages.Count == 0)
        {
            return Task.FromResult(new ToolResult(
                "No knowledge-base passages were retrieved this turn, so there is nothing to ground against. "
                + "If you are making claims that need internal sources, call search_knowledge_base first; "
                + "otherwise answer from general knowledge and say so.",
                IsError: false));
        }

        var report = GroundingEvaluator.Evaluate(answer, passages.Select(p => p.Content).ToList());

        return Task.FromResult(new ToolResult(
            FormatReport(report, passages.Count),
            IsError: false,
            Confidence: report.Score));
    }
}
