using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Infrastructure.Persistence.Configurations;

internal sealed class TicketIdConverter() : ValueConverter<TicketId, Guid>(
    id => id.Value,
    value => new TicketId(value));
