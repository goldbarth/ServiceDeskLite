using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using ServiceDeskLite.Application.Abstractions.Assistant;

namespace ServiceDeskLite.Infrastructure.Persistence.Configurations;

internal sealed class ConversationIdConverter() : ValueConverter<ConversationId, Guid>(
    id => id.Value,
    value => new ConversationId(value));
