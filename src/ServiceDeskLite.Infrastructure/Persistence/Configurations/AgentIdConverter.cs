using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using ServiceDeskLite.Domain.Agents;

namespace ServiceDeskLite.Infrastructure.Persistence.Configurations;

internal sealed class AgentIdConverter() : ValueConverter<AgentId, Guid>(
    id => id.Value,
    value => new AgentId(value));
