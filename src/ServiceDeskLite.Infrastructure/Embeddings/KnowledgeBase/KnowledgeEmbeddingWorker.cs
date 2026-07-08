using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Pgvector;

using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Infrastructure.Persistence;

namespace ServiceDeskLite.Infrastructure.Embeddings.KnowledgeBase;

/// <summary>
/// Reconciles the knowledge-base corpus into embedded, searchable chunks. Mirrors
/// <c>TicketEmbeddingWorker</c>: it chunks each article, embeds chunks that are new
/// or stale (content-hash or model mismatch) in batches, and deletes chunks whose
/// article or section no longer exists in the corpus — so editing a markdown file
/// and redeploying converges the index without manual steps. Poll-based and gated
/// on the Voyage key, exactly like ticket embeddings, so search stays optional.
/// </summary>
public sealed class KnowledgeEmbeddingWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IKnowledgeCorpus _corpus;
    private readonly VoyageOptions _options;
    private readonly IClock _clock;
    private readonly ILogger<KnowledgeEmbeddingWorker> _logger;

    public KnowledgeEmbeddingWorker(
        IServiceScopeFactory scopeFactory,
        IKnowledgeCorpus corpus,
        IOptions<VoyageOptions> options,
        IClock clock,
        ILogger<KnowledgeEmbeddingWorker> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _corpus = corpus ?? throw new ArgumentNullException(nameof(corpus));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.IsConfigured)
        {
            _logger.LogInformation(
                "Voyage:ApiKey not configured — knowledge-base embedding worker disabled, KB search unavailable.");
            return;
        }

        var interval = TimeSpan.FromSeconds(_options.PollSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PruneOrphansAsync(stoppingToken);

                // Drain everything stale before sleeping, so a fresh corpus converges
                // in one wake-up instead of one batch per poll.
                while (await EmbedNextBatchAsync(stoppingToken) > 0)
                {
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Knowledge-base embedding batch failed; retrying next poll.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>Desired chunks the corpus currently defines, keyed by their deterministic id.</summary>
    private static Dictionary<Guid, DesiredChunk> BuildDesired(IReadOnlyList<KnowledgeArticle> articles)
    {
        var desired = new Dictionary<Guid, DesiredChunk>();

        foreach (var article in articles)
        {
            foreach (var section in KnowledgeChunker.Chunk(article))
            {
                var id = KnowledgeChunker.SectionId(article.Id, section.Ordinal);
                var hash = KnowledgeChunkContent.Hash(article.Title, section.Heading, section.Content);
                desired[id] = new DesiredChunk(id, article, section, hash);
            }
        }

        return desired;
    }

    /// <summary>Removes chunks whose article/section left the corpus (renamed, deleted, restructured).</summary>
    private async Task PruneOrphansAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServiceDeskLiteDbContext>();

        var desired = BuildDesired(_corpus.Load());
        var orphans = await db.KnowledgeChunks
            .Where(c => !desired.Keys.Contains(c.Id))
            .ToListAsync(ct);

        if (orphans.Count == 0)
            return;

        db.KnowledgeChunks.RemoveRange(orphans);
        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Pruned {Count} orphaned knowledge chunk(s).", orphans.Count);
    }

    /// <returns>Number of chunks embedded in this batch.</returns>
    private async Task<int> EmbedNextBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServiceDeskLiteDbContext>();
        var embeddings = scope.ServiceProvider.GetRequiredService<IEmbeddingClient>();

        var desired = BuildDesired(_corpus.Load());
        var existing = await db.KnowledgeChunks.ToDictionaryAsync(c => c.Id, ct);

        var stale = desired.Values
            .Where(d =>
                !existing.TryGetValue(d.Id, out var e)
                || e.ContentHash != d.Hash
                || e.Model != _options.Model)
            .Take(_options.BatchSize)
            .ToList();

        if (stale.Count == 0)
            return 0;

        var texts = stale
            .Select(d => KnowledgeChunkContent.Build(d.Article.Title, d.Section.Heading, d.Section.Content))
            .ToList();

        var vectors = await embeddings.EmbedAsync(texts, EmbeddingInputType.Document, ct);
        var now = _clock.UtcNow;

        for (var i = 0; i < stale.Count; i++)
        {
            var d = stale[i];
            var vector = new Vector(vectors[i]);

            if (existing.TryGetValue(d.Id, out var entity))
            {
                entity.ArticleId = d.Article.Id;
                entity.Title = d.Article.Title;
                entity.Source = d.Article.Source;
                entity.Heading = d.Section.Heading;
                entity.Ordinal = d.Section.Ordinal;
                entity.Content = d.Section.Content;
                entity.ContentHash = d.Hash;
                entity.Model = _options.Model;
                entity.Vector = vector;
                entity.EmbeddedAt = now;
            }
            else
            {
                db.KnowledgeChunks.Add(new KnowledgeChunk
                {
                    Id = d.Id,
                    ArticleId = d.Article.Id,
                    Title = d.Article.Title,
                    Source = d.Article.Source,
                    Heading = d.Section.Heading,
                    Ordinal = d.Section.Ordinal,
                    Content = d.Section.Content,
                    ContentHash = d.Hash,
                    Model = _options.Model,
                    Vector = vector,
                    EmbeddedAt = now,
                });
            }
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Embedded {Count} knowledge chunk(s) with {Model}.", stale.Count, _options.Model);
        return stale.Count;
    }

    private sealed record DesiredChunk(Guid Id, KnowledgeArticle Article, KnowledgeSection Section, string Hash);
}
