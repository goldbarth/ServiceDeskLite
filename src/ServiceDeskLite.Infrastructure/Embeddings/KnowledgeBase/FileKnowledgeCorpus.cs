using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ServiceDeskLite.Infrastructure.Embeddings.KnowledgeBase;

/// <summary>
/// Loads the corpus from a directory of markdown files. Each file is one article;
/// the file name (without extension) is its stable id. An optional YAML-style front
/// matter block (<c>--- title / source ---</c>) supplies display metadata; without
/// it, the slug is used as the title. A missing directory yields an empty corpus
/// (logged), not an exception — the app must still boot with no knowledge base.
/// </summary>
public sealed class FileKnowledgeCorpus : IKnowledgeCorpus
{
    private const string FrontMatterFence = "---";

    private readonly KnowledgeBaseOptions _options;
    private readonly ILogger<FileKnowledgeCorpus> _logger;

    public FileKnowledgeCorpus(IOptions<KnowledgeBaseOptions> options, ILogger<FileKnowledgeCorpus> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IReadOnlyList<KnowledgeArticle> Load()
    {
        var directory = Path.IsPathRooted(_options.Path)
            ? _options.Path
            : Path.Combine(AppContext.BaseDirectory, _options.Path);

        if (!Directory.Exists(directory))
        {
            _logger.LogWarning(
                "Knowledge-base directory '{Directory}' not found — corpus is empty.", directory);
            return [];
        }

        var articles = new List<KnowledgeArticle>();

        foreach (var file in Directory.EnumerateFiles(directory, "*.md").OrderBy(f => f, StringComparer.Ordinal))
        {
            var id = Path.GetFileNameWithoutExtension(file);
            var text = File.ReadAllText(file);
            articles.Add(Parse(id, text));
        }

        return articles;
    }

    /// <summary>Parses one markdown file (optional front matter + body) into an article. Pure, for testing.</summary>
    public static KnowledgeArticle Parse(string id, string text)
    {
        var title = id;
        var source = "Article";
        var body = text;

        var normalized = text.Replace("\r\n", "\n");
        if (normalized.StartsWith(FrontMatterFence + "\n", StringComparison.Ordinal))
        {
            var end = normalized.IndexOf("\n" + FrontMatterFence, FrontMatterFence.Length, StringComparison.Ordinal);
            if (end >= 0)
            {
                var front = normalized[(FrontMatterFence.Length + 1)..end];
                body = normalized[(end + 1 + FrontMatterFence.Length)..];

                foreach (var line in front.Split('\n'))
                {
                    var colon = line.IndexOf(':');
                    if (colon <= 0)
                        continue;

                    var key = line[..colon].Trim();
                    var value = line[(colon + 1)..].Trim();

                    if (key.Equals("title", StringComparison.OrdinalIgnoreCase) && value.Length > 0)
                        title = value;
                    else if (key.Equals("source", StringComparison.OrdinalIgnoreCase) && value.Length > 0)
                        source = value;
                }
            }
        }

        return new KnowledgeArticle(id, title, source, body.Trim());
    }
}
