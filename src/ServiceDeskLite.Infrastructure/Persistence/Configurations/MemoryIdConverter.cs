using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using ServiceDeskLite.Application.Abstractions.Assistant;

namespace ServiceDeskLite.Infrastructure.Persistence.Configurations;

internal sealed class MemoryIdConverter() : ValueConverter<MemoryId, Guid>(
    id => id.Value,
    value => new MemoryId(value));
