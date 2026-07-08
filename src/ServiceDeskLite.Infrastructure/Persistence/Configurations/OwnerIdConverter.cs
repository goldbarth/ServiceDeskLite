using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using ServiceDeskLite.Application.Abstractions.Assistant;

namespace ServiceDeskLite.Infrastructure.Persistence.Configurations;

internal sealed class OwnerIdConverter() : ValueConverter<OwnerId, Guid>(
    id => id.Value,
    value => new OwnerId(value));
