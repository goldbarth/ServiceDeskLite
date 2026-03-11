using Microsoft.AspNetCore.Components;

using ServiceDeskLite.Contracts.V1.Tickets;
using ServiceDeskLite.Web.Api.V1;
using ServiceDeskLite.Web.Features.Dashboard.Components;
using ServiceDeskLite.Web.Features.Tickets.State;

namespace ServiceDeskLite.Web.Features.Tickets.Pages;

public partial class TicketBoardPage : IDisposable
{
    [Inject] private TicketBoardFeatureState BoardState { get; set; } = default!;

    private static readonly TicketStatus[] AllColumns =
    [
        TicketStatus.New,
        TicketStatus.Triaged,
        TicketStatus.InProgress,
        TicketStatus.Waiting,
        TicketStatus.Resolved,
        TicketStatus.Closed
    ];

    private static readonly IReadOnlyList<WorkflowStep> WorkflowSteps =
    [
        new("New", "Intake"),
        new("Triaged", "Route"),
        new("In Progress / Waiting", "Work"),
        new("Resolved", "Confirm"),
        new("Closed", "Archive")
    ];

    private static readonly IReadOnlyDictionary<TicketStatus, BoardColumnSpec> ColumnSpecs =
        new Dictionary<TicketStatus, BoardColumnSpec>
        {
            [TicketStatus.New] = new(
                "Incoming intake",
                "Fresh requests awaiting first review.",
                [TicketStatus.Triaged]),
            [TicketStatus.Triaged] = new(
                "Assess and route",
                "Reviewed work ready for assignment or a quick resolution path.",
                [TicketStatus.InProgress, TicketStatus.Waiting, TicketStatus.Resolved]),
            [TicketStatus.InProgress] = new(
                "Active handling",
                "Tickets currently owned and being worked by the service desk.",
                [TicketStatus.Waiting, TicketStatus.Resolved]),
            [TicketStatus.Waiting] = new(
                "External dependency",
                "Paused until customer, vendor, or another dependency responds.",
                [TicketStatus.InProgress, TicketStatus.Resolved]),
            [TicketStatus.Resolved] = new(
                "Ready to close",
                "Completed work awaiting confirmation or a possible reopen.",
                [TicketStatus.Closed, TicketStatus.InProgress]),
            [TicketStatus.Closed] = new(
                "Archived record",
                "Completed outcomes kept for traceability and audit context.",
                [])
        };

    private TicketListItemResponse? _draggingTicket;
    private TicketStatus? _hoveredStatus;
    private bool _isDragging;
    private ApiError? _moveError;

    private IEnumerable<TicketStatus> VisibleColumns => AllColumns;

    private IReadOnlyList<DashboardHeroStat> HeroStats =>
        BoardState.State is TicketBoardState.Loaded loaded
            ?
            [
                new("Workflow lanes", VisibleColumns.Count().ToString()),
                new("Board cards", loaded.Tickets.Count.ToString()),
                new("Closed lane", "Visible")
            ]
            : [];

    protected override async Task OnInitializedAsync()
    {
        BoardState.OnChanged += HandleStateChanged;
        await BoardState.LoadAsync();
    }

    public void Dispose()
        => BoardState.OnChanged -= HandleStateChanged;

    private void OnDragStart(TicketListItemResponse ticket)
    {
        _draggingTicket = ticket;
        _hoveredStatus = null;
        _isDragging = true;
        _moveError = null;
    }

    private void OnDragEnter(TicketStatus status)
    {
        if (!_isDragging)
        {
            return;
        }

        _hoveredStatus = status;
    }

    private void OnDragLeave(TicketStatus status)
    {
        if (_hoveredStatus == status)
        {
            _hoveredStatus = null;
        }
    }

    private void OnDragEnd()
    {
        _draggingTicket = null;
        _hoveredStatus = null;
        _isDragging = false;
    }

    private async Task OnDrop(TicketStatus targetStatus)
    {
        if (_draggingTicket is null)
        {
            return;
        }

        if (!CanDropTo(targetStatus))
        {
            OnDragEnd();
            return;
        }

        var ticket = _draggingTicket;
        _draggingTicket = null;
        _hoveredStatus = null;
        _isDragging = false;

        _moveError = await BoardState.MoveAsync(ticket.Id, targetStatus);
    }

    private static IReadOnlyList<TicketListItemResponse> TicketsForColumn(
        IReadOnlyList<TicketListItemResponse> tickets,
        TicketStatus status)
        => [.. tickets.Where(ticket => ticket.Status == status)];

    private static string FormatStatus(TicketStatus status)
        => status switch
        {
            TicketStatus.InProgress => "In Progress",
            _ => status.ToString()
        };

    private static string FormatPriority(TicketPriority priority)
        => priority.ToString();

    private static string FormatTicketRef(Guid id)
        => $"#{id:N}"[..7].ToUpperInvariant();

    private static string BuildCardSummary(TicketListItemResponse ticket)
    {
        if (!string.IsNullOrWhiteSpace(ticket.Description))
        {
            return Truncate(ticket.Description.Trim(), 110);
        }

        return ticket.DueAt is not null
            ? $"Due {ticket.DueAt.Value.ToLocalTime():dd MMM yyyy}"
            : $"Created {ticket.CreatedAt.ToLocalTime():dd MMM yyyy}";
    }

    private static string FormatBoardDate(TicketListItemResponse ticket)
        => ticket.DueAt is not null
            ? $"Due {ticket.DueAt.Value.ToLocalTime():dd MMM}"
            : $"Created {ticket.CreatedAt.ToLocalTime():dd MMM}";

    private static string PriorityClass(TicketPriority priority)
        => priority switch
        {
            TicketPriority.Low => "board-priority--low",
            TicketPriority.Medium => "board-priority--medium",
            TicketPriority.High => "board-priority--high",
            TicketPriority.Critical => "board-priority--critical",
            _ => string.Empty
        };

    private string ColumnClass(TicketStatus status)
    {
        var classes = new List<string>();

        if (!_isDragging || _draggingTicket is null)
        {
            return string.Empty;
        }

        if (_draggingTicket.Status == status)
        {
            classes.Add("board-column--origin");
        }
        else if (CanDropTo(status))
        {
            classes.Add("board-column--can-drop");
        }
        else
        {
            classes.Add("board-column--blocked");
        }

        if (_hoveredStatus == status)
        {
            classes.Add("board-column--drop-active");
        }

        return string.Join(" ", classes);
    }

    private string DropZoneClass(TicketStatus status)
    {
        if (!_isDragging || _draggingTicket is null)
        {
            return "board-dropzone";
        }

        var classes = new List<string> { "board-dropzone" };

        if (_draggingTicket.Status == status)
        {
            classes.Add("board-dropzone--origin");
        }
        else if (CanDropTo(status))
        {
            classes.Add("board-dropzone--allowed");
        }
        else
        {
            classes.Add("board-dropzone--blocked");
        }

        if (_hoveredStatus == status)
        {
            classes.Add("board-dropzone--active");
        }

        return string.Join(" ", classes);
    }

    private string DropZoneText(TicketStatus status)
    {
        if (_draggingTicket is null)
        {
            return string.Empty;
        }

        if (_draggingTicket.Status == status)
        {
            return "Current lane";
        }

        return CanDropTo(status)
            ? $"Drop here to move to {FormatStatus(status)}"
            : $"Not allowed from {FormatStatus(_draggingTicket.Status)}";
    }

    private static string AllowedNextText(TicketStatus status)
    {
        var allowed = ColumnSpecs[status].AllowedNext;
        return allowed.Count == 0
            ? "Final state"
            : string.Join(", ", allowed.Select(FormatStatus));
    }

    private static string EmptyStateText(TicketStatus status)
        => status switch
        {
            TicketStatus.New => "No incoming tickets in the intake lane.",
            TicketStatus.Triaged => "Nothing is waiting for routing.",
            TicketStatus.InProgress => "No tickets are actively owned right now.",
            TicketStatus.Waiting => "Nothing is currently blocked externally.",
            TicketStatus.Resolved => "No tickets are awaiting confirmation.",
            TicketStatus.Closed => "No archived tickets are currently visible in this lane.",
            _ => "No tickets in this lane."
        };

    private static bool IsOverdue(TicketListItemResponse ticket)
        => ticket.DueAt is not null
            && ticket.DueAt.Value < DateTimeOffset.UtcNow
            && ticket.Status is not TicketStatus.Resolved and not TicketStatus.Closed;

    private static string DateClass(TicketListItemResponse ticket)
        => IsOverdue(ticket)
            ? "board-card__meta-item board-card__meta-item--alert"
            : "board-card__meta-item";

    private bool CanDropTo(TicketStatus targetStatus)
        => _draggingTicket is not null
            && _draggingTicket.Status != targetStatus
            && ColumnSpecs[_draggingTicket.Status].AllowedNext.Contains(targetStatus);

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return $"{value[..(maxLength - 3)].TrimEnd()}...";
    }

    private void HandleStateChanged()
        => InvokeAsync(StateHasChanged);

    private sealed record WorkflowStep(string Label, string Caption);

    private sealed record BoardColumnSpec(
        string WorkflowHint,
        string Description,
        IReadOnlyList<TicketStatus> AllowedNext);
}
