using Microsoft.AspNetCore.Components;

using MudBlazor;

using ServiceDeskLite.Contracts.V1.Tickets;
using ServiceDeskLite.Web.Api.V1;
using ServiceDeskLite.Web.Features.Tickets.Components;
using ServiceDeskLite.Web.Features.Tickets.State;

namespace ServiceDeskLite.Web.Features.Tickets.Pages;

public partial class TicketDetailsPage
{
    [Inject] private ITicketsApiClient TicketsApi { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    [Inject] private TicketsListFeatureState TicketsListState { get; set; } = default!;

    [Parameter] public Guid Id { get; set; }

    private bool _isLoading;
    private ApiError? _error;
    private TicketResponse? _ticket;

    private string? _commentAuthor;
    private string? _commentContent;
    private bool _isSubmittingComment;
    private ApiError? _commentError;

    private TicketDetailsTab _activeTab = TicketDetailsTab.Details;

    private int CommentCount
        => _ticket?.Conversation.Count(x => x.Kind == ConversationItemKind.Comment) ?? 0;

    private int HistoryCount
        => _ticket?.Conversation.Count(x => x.Kind == ConversationItemKind.SystemEvent) ?? 0;

    protected override async Task OnParametersSetAsync()
    {
        _isLoading = true;
        _error = null;
        _ticket = null;
        _commentError = null;
        _activeTab = TicketDetailsTab.Details;

        await LoadPageAsync();

        _isLoading = false;
    }

    private async Task LoadPageAsync()
    {
        var result = await TicketsApi.GetByIdAsync(Id);

        if (result.IsSuccess)
        {
            _ticket = result.Value;
        }
        else
        {
            _error = result.Error;
        }
    }

    private void SelectTab(TicketDetailsTab tab)
        => _activeTab = tab;

    private string TabClass(TicketDetailsTab tab)
        => _activeTab == tab ? "ticket-tabs__tab is-active" : "ticket-tabs__tab";

    private async Task OpenChangeStatusDialogAsync()
    {
        var parameters = new DialogParameters<ChangeStatusDialog>
        {
            { x => x.TicketId, _ticket!.Id },
            { x => x.AllowedStatuses, _ticket.AllowedTransitions }
        };

        var dialog = await DialogService.ShowAsync<ChangeStatusDialog>(
            "Change Ticket Status", parameters);

        var result = await dialog.Result;

        if (result is { Canceled: false, Data: TicketResponse updated })
        {
            _ticket = updated;
            TicketsListState.Invalidate();
            Snackbar.Add("Status updated.", Severity.Success);
        }
    }

    private async Task OpenAssignDialogAsync()
    {
        var parameters = new DialogParameters<AssignTicketDialog>
        {
            { x => x.TicketId, _ticket!.Id },
            { x => x.CurrentAssignee, _ticket.Assignee }
        };

        var dialog = await DialogService.ShowAsync<AssignTicketDialog>(
            "Assign Ticket", parameters);

        var result = await dialog.Result;

        if (result is { Canceled: false, Data: TicketResponse updated })
        {
            _ticket = updated;
            TicketsListState.Invalidate();
            Snackbar.Add("Assignee updated.", Severity.Success);
        }
    }

    private async Task SubmitCommentAsync()
    {
        if (string.IsNullOrWhiteSpace(_commentContent))
        {
            return;
        }

        _isSubmittingComment = true;
        _commentError = null;

        var result = await TicketsApi.AddCommentAsync(
            _ticket!.Id,
            new AddCommentRequest(
                Content: _commentContent.Trim(),
                Author: string.IsNullOrWhiteSpace(_commentAuthor) ? null : _commentAuthor.Trim()));

        _isSubmittingComment = false;

        if (result.IsSuccess)
        {
            await RefreshTicketAsync();
            TicketsListState.Invalidate();

            _commentContent = null;
            _commentAuthor = null;
            _activeTab = TicketDetailsTab.Comments;

            Snackbar.Add("Comment added.", Severity.Success);
            return;
        }

        _commentError = result.Error;
    }

    private async Task RefreshTicketAsync()
    {
        var result = await TicketsApi.GetByIdAsync(Id);

        if (result.IsSuccess)
        {
            _ticket = result.Value;
        }
    }

    private static string BuildHeaderSummary(string description)
        => Truncate(description.Trim(), 220);

    private static string FormatStatus(TicketStatus status)
        => status switch
        {
            TicketStatus.InProgress => "In Progress",
            _ => status.ToString()
        };

    private static string FormatPriority(TicketPriority priority)
        => priority.ToString();

    private static string StatusBadgeClass(TicketStatus status)
        => status switch
        {
            TicketStatus.New => "ticket-detail-badge--status-new",
            TicketStatus.Triaged => "ticket-detail-badge--status-triaged",
            TicketStatus.InProgress => "ticket-detail-badge--status-inprogress",
            TicketStatus.Waiting => "ticket-detail-badge--status-waiting",
            TicketStatus.Resolved => "ticket-detail-badge--status-resolved",
            TicketStatus.Closed => "ticket-detail-badge--status-closed",
            _ => string.Empty
        };

    private static string PriorityBadgeClass(TicketPriority priority)
        => priority switch
        {
            TicketPriority.Low => "ticket-detail-badge--priority-low",
            TicketPriority.Medium => "ticket-detail-badge--priority-medium",
            TicketPriority.High => "ticket-detail-badge--priority-high",
            TicketPriority.Critical => "ticket-detail-badge--priority-critical",
            _ => string.Empty
        };

    private static string FormatDateTime(DateTimeOffset value)
        => value.ToLocalTime().ToString("dd MMM yyyy, HH:mm");

    private static string FormatDue(TicketResponse ticket)
        => ticket.DueAt is null ? "No due date" : FormatDateTime(ticket.DueAt.Value);

    private static string DueValueClass(TicketResponse ticket)
        => ticket.IsOverdue
            ? "ticket-context__value ticket-context__value--alert"
            : "ticket-context__value";

    private static string DisplayAuthor(string? author)
        => string.IsNullOrWhiteSpace(author) ? "Anonymous" : author.Trim();

    private static string CommentInitials(string? author)
    {
        var value = DisplayAuthor(author);
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
        {
            return "AN";
        }

        if (parts.Length == 1)
        {
            return value[..Math.Min(2, value.Length)].ToUpperInvariant();
        }

        return string.Concat(parts[0][0], parts[1][0]).ToUpperInvariant();
    }

    private static string EventIcon(AuditEventPayload payload)
        => payload switch
        {
            TicketCreatedPayload => Icons.Material.Outlined.AddCircleOutline,
            TicketStatusChangedPayload => Icons.Material.Outlined.SyncAlt,
            TicketAssigneeChangedPayload => Icons.Material.Outlined.PersonOutline,
            TicketCommentAddedPayload => Icons.Material.Outlined.ModeComment,
            _ => Icons.Material.Outlined.Info
        };

    private static string HistoryMarkerClass(AuditEventPayload payload)
        => payload switch
        {
            TicketCreatedPayload => "ticket-history__marker--created",
            TicketStatusChangedPayload => "ticket-history__marker--status",
            TicketAssigneeChangedPayload => "ticket-history__marker--assignee",
            TicketCommentAddedPayload => "ticket-history__marker--comment",
            _ => "ticket-history__marker--default"
        };

    private static string FormatEventType(AuditEventPayload payload)
        => payload switch
        {
            TicketCreatedPayload => "Ticket created",
            TicketStatusChangedPayload => "Status changed",
            TicketAssigneeChangedPayload => "Assignee changed",
            TicketCommentAddedPayload => "Comment added",
            _ => "Audit event"
        };

    private static string FormatEventSummary(AuditEventResponse auditEvent)
        => auditEvent.Payload switch
        {
            TicketCreatedPayload created => $"{created.Title} opened with {FormatPriority(created.Priority)} priority.",
            TicketStatusChangedPayload statusChanged => $"{FormatStatus(statusChanged.FromStatus)} -> {FormatStatus(statusChanged.ToStatus)}.",
            TicketAssigneeChangedPayload assigneeChanged => FormatAssigneeChange(assigneeChanged),
            TicketCommentAddedPayload commentAdded => $"Internal note added by {DisplayActor(commentAdded.Author)}.",
            RawAuditEventPayload => "Raw audit payload captured.",
            _ => "Unknown workflow event."
        };

    private static string FormatAssigneeChange(TicketAssigneeChangedPayload payload)
    {
        if (!string.IsNullOrWhiteSpace(payload.NewAssignee) && !string.IsNullOrWhiteSpace(payload.PreviousAssignee))
        {
            return $"Reassigned from {payload.PreviousAssignee} to {payload.NewAssignee}.";
        }

        if (!string.IsNullOrWhiteSpace(payload.NewAssignee))
        {
            return $"Assigned to {payload.NewAssignee}.";
        }

        return $"Unassigned from {payload.PreviousAssignee ?? "previous owner"}.";
    }

    private static string DisplayActor(string? actor)
        => string.IsNullOrWhiteSpace(actor) ? "the service desk" : actor.Trim();

    private static string FormatStatus(string status)
        => status switch
        {
            "InProgress" => "In Progress",
            _ => status
        };

    private static string FormatPriority(string priority)
        => priority switch
        {
            "Low" => "Low",
            "Medium" => "Medium",
            "High" => "High",
            "Critical" => "Critical",
            _ => priority
        };

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return $"{value[..(maxLength - 3)].TrimEnd()}...";
    }

    private enum TicketDetailsTab
    {
        Details,
        Comments,
        History
    }
}