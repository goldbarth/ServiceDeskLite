using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ServiceDeskLite.Domain.Outbox;

namespace ServiceDeskLite.Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageIdConverter() : ValueConverter<OutboxMessageId, Guid>(
    id => id.Value,
    value => new OutboxMessageId(value));