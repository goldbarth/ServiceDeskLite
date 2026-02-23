using ServiceDeskLite.Contracts.V1.Common;
using ServiceDeskLite.Contracts.V1.Tickets;

using AppSortDirection = ServiceDeskLite.Application.Tickets.Shared.SortDirection;
using AppTicketSortField = ServiceDeskLite.Application.Tickets.Shared.TicketSortField;
using DomainTicketPriority = ServiceDeskLite.Domain.Tickets.TicketPriority;
using DomainTicketStatus = ServiceDeskLite.Domain.Tickets.TicketStatus;

namespace ServiceDeskLite.Api.Mapping.Tickets;

internal static class TicketEnumMapping
{
    public static DomainTicketPriority ToDomain(this TicketPriority value) => value switch
    {
        TicketPriority.Low => DomainTicketPriority.Low,
        TicketPriority.Medium => DomainTicketPriority.Medium,
        TicketPriority.High => DomainTicketPriority.High,
        TicketPriority.Critical => DomainTicketPriority.Critical,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported TicketPriority.")
    };
    
    public static AppTicketSortField ToApplication(this TicketSortField value) => value switch
    {
        TicketSortField.CreatedAt => AppTicketSortField.CreatedAt,
        TicketSortField.DueAt     => AppTicketSortField.DueAt,
        TicketSortField.Priority  => AppTicketSortField.Priority,
        TicketSortField.Status    => AppTicketSortField.Status,
        TicketSortField.Title     => AppTicketSortField.Title,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported TicketSortField.")
    };

    public static AppSortDirection ToApplication(this SortDirection value) => value switch
    {
        SortDirection.Asc  => AppSortDirection.Asc,
        SortDirection.Desc => AppSortDirection.Desc,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported SortDirection.")
    };

    public static DomainTicketStatus ToDomain(this TicketStatus value) => value switch
    {
        TicketStatus.New        => DomainTicketStatus.New,
        TicketStatus.Triaged    => DomainTicketStatus.Triaged,
        TicketStatus.InProgress => DomainTicketStatus.InProgress,
        TicketStatus.Waiting    => DomainTicketStatus.Waiting,
        TicketStatus.Resolved   => DomainTicketStatus.Resolved,
        TicketStatus.Closed     => DomainTicketStatus.Closed,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported TicketStatus.")
    };
}
