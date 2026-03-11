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

    private IReadOnlyList<AuditEventResponse>? _auditEvents;
    private ApiError? _auditEventsError;
    private TicketDetailsTab _activeTab = TicketDetailsTab.Details;

    private int CommentCount => _ticket?.Comments.Count ?? 0;
    private int HistoryCount => _auditEvents?.Count ?? 0;

    private IReadOnlyList<ConversationItem> ConversationItems
    {
        get
        {
            if (_ticket is null)
            {
                return [];
            }

            var items = new List<ConversationItem>();

            items.AddRange(_ticket.Comments.Select(comment =>
                new ConversationItem(comment.CreatedAt, ConversationItemKind.Comment, comment, null)));

            if (_auditEvents is not null)
            {
                items.AddRange(_auditEvents
                    .Where(auditEvent => auditEvent.Payload is not TicketCommentAddedPayload)
                    .Select(auditEvent => new ConversationItem(auditEvent.OccurredAt, ConversationItemKind.SystemEvent, null, auditEvent)));
            }

            return [.. items
                .OrderBy(item => item.Timestamp)
                .ThenBy(item => item.Kind)];
        }
    }

    private string WorkflowActionNote
        => _ticket is null
            ? string.Empty
            : _ticket.AllowedTransitions.Count == 0
                ? "No further status transition is currently available for this ticket."
                : $"Next workflow options: {string.Join(", ", _ticket.AllowedTransitions.Select(FormatStatus))}.";

    private string DetailsSummaryText
        => _ticket is null
            ? string.Empty
            : _ticket.Status switch
            {
                TicketStatus.New => "Review the request, confirm the issue statement, and move it into triage once it is ready for routing.",
                TicketStatus.Triaged => "The ticket is ready for assignment, progress work, or direct resolution if the outcome is already clear.",
                TicketStatus.InProgress => "Active work is underway. Keep the latest context in the conversation thread and track the due date closely.",
                TicketStatus.Waiting => "The ticket is currently blocked by an external dependency. Document the blocker and move it forward once a response arrives.",
                TicketStatus.Resolved => "Resolution is recorded. Confirm the outcome and close the ticket when no further action is needed.",
                TicketStatus.Closed => "The workflow is complete. Use history and comments as the factual record of what happened.",
                _ => string.Empty
            };

    private IReadOnlyList<string> SuggestedNextSteps
    {
        get
        {
            if (_ticket is null)
            {
                return [];
            }

            var suggestions = new List<string>();

            if (_ticket.Assignee is null && _ticket.Status is not TicketStatus.Closed)
            {
                suggestions.Add("Assign an owner so responsibility and next handling steps are explicit.");
            }

            if (_ticket.DueAt is null && _ticket.Status is not TicketStatus.Resolved and not TicketStatus.Closed)
            {
                suggestions.Add("Add a due date if the ticket should be tracked against a service target or external commitment.");
            }

            if (_ticket.AllowedTransitions.Count > 0)
            {
                suggestions.Add($"Prepare the next workflow move: {string.Join(", ", _ticket.AllowedTransitions.Select(FormatStatus))}.");
            }

            if (_ticket.Status == TicketStatus.Waiting)
            {
                suggestions.Add("Record the blocker clearly in the notes and move the ticket back to In Progress once the dependency responds.");
            }

            if (_ticket.Status == TicketStatus.Resolved)
            {
                suggestions.Add("Validate the outcome before closing, or reopen to In Progress if follow-up work is required.");
            }

            if (suggestions.Count == 0)
            {
                suggestions.Add("No immediate follow-up is suggested. Use comments and history for recordkeeping.");
            }

            return suggestions;
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        _isLoading = true;
        _error = null;
        _ticket = null;
        _commentError = null;
        _auditEvents = null;
        _auditEventsError = null;
        _activeTab = TicketDetailsTab.Details;

        await LoadPageAsync();

        _isLoading = false;
    }

    private async Task LoadPageAsync()
    {
        var ticketResult = await TicketsApi.GetByIdAsync(Id);
        var auditResult = await TicketsApi.GetAuditEventsAsync(Id);

        if (ticketResult.IsSuccess)
        {
            _ticket = ticketResult.Value;
        }
        else
        {
            _error = ticketResult.Error;
        }

        if (auditResult.IsSuccess)
        {
            _auditEvents = auditResult.Value;
        }
        else
        {
            _auditEventsError = auditResult.Error;
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
            await RefreshAuditEventsAsync();
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
            await RefreshAuditEventsAsync();
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
            await RefreshAuditEventsAsync();
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

    private async Task RefreshAuditEventsAsync()
    {
        var result = await TicketsApi.GetAuditEventsAsync(Id);

        if (result.IsSuccess)
        {
            _auditEvents = result.Value;
            _auditEventsError = null;
        }
        else
        {
            _auditEventsError = result.Error;
        }
    }

    private static string FormatTicketRef(Guid id)
        => $"#{id:N}"[..7].ToUpperInvariant();

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

    private static bool IsOverdue(TicketResponse ticket)
        => ticket.DueAt is not null
            && ticket.DueAt.Value < DateTimeOffset.UtcNow
            && ticket.Status is not TicketStatus.Resolved and not TicketStatus.Closed;

    private static string DueValueClass(TicketResponse ticket)
        => IsOverdue(ticket)
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

    private enum ConversationItemKind
    {
        Comment,
        SystemEvent
    }

    private sealed record ConversationItem(
        DateTimeOffset Timestamp,
        ConversationItemKind Kind,
        CommentResponse? Comment,
        AuditEventResponse? Event);
}
