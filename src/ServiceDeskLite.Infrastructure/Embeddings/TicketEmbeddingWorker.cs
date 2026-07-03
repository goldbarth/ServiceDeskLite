using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Pgvector;

using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Infrastructure.Persistence;

namespace ServiceDeskLite.Infrastructure.Embeddings;

/// <summary>
/// Single mechanism for all embedding writes: polls for tickets whose
/// embedding is missing (new/seeded/backfill) or stale (title/description
/// edited → content hash mismatch), embeds them in batches and upserts.
/// Polling instead of create-hooks keeps the write path free of a network
/// dependency — search is eventually consistent by design.
/// </summary>
public sealed class TicketEmbeddingWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly VoyageOptions _options;
    private readonly IClock _clock;
    private readonly ILogger<TicketEmbeddingWorker> _logger;

    public TicketEmbeddingWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<VoyageOptions> options,
        IClock clock,
        ILogger<TicketEmbeddingWorker> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.IsConfigured)
        {
            _logger.LogInformation(
                "Voyage:ApiKey not configured — ticket embedding worker disabled, semantic search unavailable.");
            return;
        }

        var interval = TimeSpan.FromSeconds(_options.PollSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Drain everything stale before sleeping, so a fresh seed or bulk
                // import converges in one wake-up instead of one batch per poll.
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
                _logger.LogError(ex, "Ticket embedding batch failed; retrying next poll.");
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

    /// <returns>Number of tickets embedded in this batch.</returns>
    private async Task<int> EmbedNextBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServiceDeskLiteDbContext>();
        var embeddings = scope.ServiceProvider.GetRequiredService<IEmbeddingClient>();

        // Load only the fields needed for the diff; fine at this data size.
        // At real scale this would become a keyset-paged query.
        var tickets = await db.Tickets
            .Select(t => new { t.Id, t.Title, t.Description })
            .ToListAsync(ct);

        var existing = await db.TicketEmbeddings
            .ToDictionaryAsync(e => e.TicketId, ct);

        var stale = tickets
            .Where(t =>
                !existing.TryGetValue(t.Id, out var e)
                || e.ContentHash != TicketEmbeddingContent.Hash(t.Title, t.Description)
                || e.Model != _options.Model)
            .Take(_options.BatchSize)
            .ToList();

        if (stale.Count == 0)
            return 0;

        var texts = stale
            .Select(t => TicketEmbeddingContent.Build(t.Title, t.Description))
            .ToList();

        var vectors = await embeddings.EmbedAsync(texts, EmbeddingInputType.Document, ct);
        var now = _clock.UtcNow;

        for (var i = 0; i < stale.Count; i++)
        {
            var ticket = stale[i];
            var vector = new Vector(vectors[i]);
            var hash = TicketEmbeddingContent.Hash(ticket.Title, ticket.Description);

            if (existing.TryGetValue(ticket.Id, out var entity))
            {
                entity.Vector = vector;
                entity.ContentHash = hash;
                entity.Model = _options.Model;
                entity.EmbeddedAt = now;
            }
            else
            {
                db.TicketEmbeddings.Add(new TicketEmbedding
                {
                    TicketId = ticket.Id,
                    Vector = vector,
                    ContentHash = hash,
                    Model = _options.Model,
                    EmbeddedAt = now,
                });
            }
        }

        await db.SaveChangesAsync(ct);

        _logger.LogInformation("Embedded {Count} ticket(s) with {Model}.", stale.Count, _options.Model);
        return stale.Count;
    }
}
