using System.Security.Cryptography;
using System.Text;

namespace ServiceDeskLite.Infrastructure.Embeddings.KnowledgeBase;

/// <summary>
/// Splits an article into section chunks on level-2 (<c>## </c>) headings. Content
/// before the first heading becomes an "Overview" chunk so nothing is dropped.
/// Static and side-effect free: the worker and its tests share it so the stored
/// chunks always match what was embedded.
/// </summary>
public static class KnowledgeChunker
{
    private const string OverviewHeading = "Overview";

    public static IReadOnlyList<KnowledgeSection> Chunk(KnowledgeArticle article)
    {
        var sections = new List<KnowledgeSection>();
        var heading = OverviewHeading;
        var body = new StringBuilder();
        var ordinal = 0;

        void Flush()
        {
            var content = body.ToString().Trim();
            if (content.Length > 0)
                sections.Add(new KnowledgeSection(ordinal++, heading, content));

            body.Clear();
        }

        foreach (var line in article.Body.Split('\n'))
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                Flush();
                heading = line[3..].Trim();
                continue;
            }

            body.Append(line).Append('\n');
        }

        Flush();
        return sections;
    }

    /// <summary>
    /// Stable chunk id derived from the article id + ordinal, so re-running the
    /// worker upserts the same row instead of duplicating it. Deterministic across
    /// process restarts — unlike <c>Guid.NewGuid</c>.
    /// </summary>
    public static Guid SectionId(string articleId, int ordinal)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes($"{articleId}#{ordinal}"));
        return new Guid(bytes);
    }
}
