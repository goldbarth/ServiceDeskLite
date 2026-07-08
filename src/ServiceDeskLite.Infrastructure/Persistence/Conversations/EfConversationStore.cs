using Microsoft.EntityFrameworkCore;

using ServiceDeskLite.Application.Abstractions.Assistant;

namespace ServiceDeskLite.Infrastructure.Persistence.Conversations;

public sealed class EfConversationStore : IConversationStore
{
    private readonly ServiceDeskLiteDbContext _db;

    public EfConversationStore(ServiceDeskLiteDbContext db)
        => _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task<IReadOnlyList<ConversationMessage>> GetAsync(
        ConversationId id, OwnerId owner, CancellationToken ct)
        => await _db.Set<ConversationMessageRecord>()
            .AsNoTracking()
            .Where(m => m.ConversationId == id && m.Owner == owner)
            .OrderBy(m => m.Sequence)
            .Select(m => new ConversationMessage(m.Sequence, m.Role, m.Content, m.CreatedAt))
            .ToListAsync(ct);

    public async Task AppendAsync(
        ConversationId id, OwnerId owner, IReadOnlyList<ConversationMessage> messages, CancellationToken ct)
    {
        if (messages.Count == 0)
            return;

        foreach (var m in messages)
        {
            _db.Add(new ConversationMessageRecord
            {
                ConversationId = id,
                Sequence = m.Sequence,
                Owner = owner,
                Role = m.Role,
                Content = m.Content,
                CreatedAt = m.CreatedAt,
            });
        }

        await _db.SaveChangesAsync(ct);
    }
}
