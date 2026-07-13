using System.Text;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using ServiceDeskLite.Contracts.V1.Agents;
using ServiceDeskLite.Contracts.V1.Assistant;
using ServiceDeskLite.Contracts.V1.Tickets;
using ServiceDeskLite.Web.Api.V1;
using ServiceDeskLite.Web.Api.V1.Assistant;
using ServiceDeskLite.Web.Features.Tickets.State;

namespace ServiceDeskLite.Web.Features.Tickets.Pages;

public partial class TicketDetailsPage : IDisposable
{
    [Inject] private ITicketsApiClient TicketsApi { get; set; } = default!;
    [Inject] private ITicketSummaryApiClient SummaryApi { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    [Inject] private TicketsListFeatureState TicketsListState { get; set; } = default!;

    [Parameter] public Guid Id { get; set; }

    private bool _isLoading;
    private ApiError? _error;
    private TicketResponse? _ticket;

    private bool _isChangingStatus;
    private bool _isAssigning;

    private string? _commentAuthor;
    private string? _commentContent;
    private bool _isSubmittingComment;
    private ApiError? _commentError;

    // Roster for the comment author picker. No real login yet — in production the
    // author would be the signed-in user rather than a chosen account.
    private IReadOnlyList<AgentResponse> _agents = [];
    private bool _isLoadingAgents = true;

    private MudForm _editForm = default!;
    private bool _isEditing;
    private bool _isEditFormValid;
    private bool _isSavingEdit;
    private ApiError? _editError;
    private string _editTitle = string.Empty;
    private string _editDescription = string.Empty;
    private TicketPriority _editPriority;
    private DateTime? _editDueAt;

    // The summary costs a model call, so it is streamed once on first open of its tab and
    // then kept for the lifetime of this page — switching tabs must not re-generate it.
    private readonly Dictionary<TicketSummarySection, StringBuilder> _summaryBuffers = [];
    private CancellationTokenSource? _summaryCts;
    private bool _summaryRequested;
    private bool _isStreamingSummary;
    private ApiError? _summaryError;

    private bool CanEdit => _ticket is not null && _ticket.Status != TicketStatus.Closed;

    private TicketDetailsTab _activeTab = TicketDetailsTab.Details;

    private Dictionary<TicketSummarySection, string> SummaryContent()
        => _summaryBuffers.ToDictionary(x => x.Key, x => x.Value.ToString());

    private int CommentCount
        => _ticket?.Conversation.Count(x => x.Kind == ConversationItemKind.Comment) ?? 0;

    private int HistoryCount
        => _ticket?.Conversation.Count(x => x.Kind == ConversationItemKind.SystemEvent) ?? 0;

    protected override async Task OnInitializedAsync()
    {
        var result = await TicketsApi.GetAgentsAsync();
        if (result.IsSuccess)
        {
            _agents = result.Value!;
        }

        _isLoadingAgents = false;
    }

    protected override async Task OnParametersSetAsync()
    {
        _isLoading = true;
        _error = null;
        _ticket = null;
        _commentError = null;
        _activeTab = TicketDetailsTab.Details;
        _isEditing = false;
        _editError = null;

        ResetSummary();

        await LoadPageAsync();

        _isLoading = false;
    }

    /// <summary>Abandons any in-flight summary: it describes the ticket we just navigated away from.</summary>
    private void ResetSummary()
    {
        _summaryCts?.Cancel();
        _summaryCts?.Dispose();
        _summaryCts = null;

        _summaryBuffers.Clear();
        _summaryRequested = false;
        _isStreamingSummary = false;
        _summaryError = null;
    }

    private async Task StreamSummaryAsync()
    {
        _summaryRequested = true;
        _isStreamingSummary = true;
        _summaryError = null;

        _summaryCts = new CancellationTokenSource();
        var ct = _summaryCts.Token;

        try
        {
            await foreach (var evt in SummaryApi.StreamAsync(Id, ct))
            {
                if (evt.EventType == TicketSummaryStreamEvent.ErrorEvent)
                {
                    _summaryError = new ApiError { Title = "Summary unavailable", Detail = evt.Message };
                    break;
                }

                if (evt is { EventType: TicketSummaryStreamEvent.DeltaEvent, Section: { } section, Text: { } text })
                {
                    if (!_summaryBuffers.TryGetValue(section, out var buffer))
                        _summaryBuffers[section] = buffer = new StringBuilder();

                    buffer.Append(text);
                    StateHasChanged();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Navigated away or ticket changed — the partial summary is discarded with the page.
        }
        finally
        {
            _isStreamingSummary = false;
        }
    }

    public void Dispose()
    {
        _summaryCts?.Cancel();
        _summaryCts?.Dispose();
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

    private async Task SelectTabAsync(TicketDetailsTab tab)
    {
        _activeTab = tab;

        if (tab == TicketDetailsTab.Summary && !_summaryRequested)
            await StreamSummaryAsync();
    }

    private string TabClass(TicketDetailsTab tab)
        => _activeTab == tab ? "ticket-tabs__tab is-active" : "ticket-tabs__tab";

    // Status and assignee change inline through popovers anchored to the header, so the
    // ticket stays open and scrolled where it was. Both hit the same command handlers the
    // deleted dialogs used; the errors surface as a snackbar instead of a modal panel.
    private async Task ChangeStatusAsync(TicketStatus status)
    {
        if (_ticket is null || _isChangingStatus || status == _ticket.Status)
            return;

        _isChangingStatus = true;

        var result = await TicketsApi.ChangeStatusAsync(
            _ticket.Id, new ChangeTicketStatusRequest(status));

        _isChangingStatus = false;

        if (result.IsSuccess)
        {
            _ticket = result.Value;
            TicketsListState.Invalidate();
            Snackbar.Add("Status updated.", Severity.Success);
            return;
        }

        Snackbar.Add(result.Error?.Detail ?? "Could not update status.", Severity.Error);
    }

    private async Task AssignAsync(Guid? agentId)
    {
        if (_ticket is null || _isAssigning)
            return;

        // No-op if the picked agent is already the assignee (roster names are unique).
        var currentId = _agents.FirstOrDefault(a => a.Name == _ticket.Assignee)?.Id;
        if (agentId == currentId)
            return;

        _isAssigning = true;

        var result = await TicketsApi.AssignAsync(
            _ticket.Id, new AssignTicketRequest(agentId));

        _isAssigning = false;

        if (result.IsSuccess)
        {
            _ticket = result.Value;
            TicketsListState.Invalidate();
            Snackbar.Add("Assignee updated.", Severity.Success);
            return;
        }

        Snackbar.Add(result.Error?.Detail ?? "Could not update assignee.", Severity.Error);
    }

    private void BeginEdit()
    {
        if (!CanEdit)
        {
            return;
        }

        _editTitle = _ticket!.Title;
        _editDescription = _ticket.Description;
        _editPriority = _ticket.Priority;
        _editDueAt = _ticket.DueAt?.LocalDateTime;
        _editError = null;
        _isEditing = true;
    }

    private void CancelEdit()
    {
        _isEditing = false;
        _editError = null;
    }

    private async Task SaveEditAsync()
    {
        await _editForm.Validate();

        if (!_isEditFormValid)
        {
            return;
        }

        var request = BuildUpdateRequest();
        if (request is null)
        {
            // Nothing changed — leave edit mode without a redundant round-trip.
            _isEditing = false;
            Snackbar.Add("No changes to save.", Severity.Info);
            return;
        }

        _isSavingEdit = true;
        _editError = null;

        var result = await TicketsApi.UpdateAsync(_ticket!.Id, request);

        _isSavingEdit = false;

        if (result.IsSuccess)
        {
            _ticket = result.Value;
            TicketsListState.Invalidate();
            _isEditing = false;
            Snackbar.Add("Ticket updated.", Severity.Success);
            return;
        }

        _editError = result.Error;
    }

    // Partial update: only send fields that actually changed, matching
    // UpdateTicketHandler semantics (null = keep current). Returns null when
    // nothing changed. Clearing an existing due date is not expressible via the
    // partial contract and is intentionally not attempted here.
    private UpdateTicketRequest? BuildUpdateRequest()
    {
        var newTitle = _editTitle.Trim();
        var newDescription = _editDescription.Trim();

        var title = newTitle != _ticket!.Title ? newTitle : null;
        var description = newDescription != _ticket.Description ? newDescription : null;
        TicketPriority? priority = _editPriority != _ticket.Priority ? _editPriority : null;

        DateTimeOffset? dueAt = null;
        if (_editDueAt is { } due)
        {
            // The date picker's value carries a local Kind; force Unspecified so the
            // DateTimeOffset ctor accepts the zero offset (mirrors the create flow).
            var date = DateTime.SpecifyKind(due.Date, DateTimeKind.Unspecified);
            var candidate = new DateTimeOffset(date, TimeSpan.Zero);
            if (_ticket.DueAt is null || candidate.Date != _ticket.DueAt.Value.Date)
            {
                dueAt = candidate;
            }
        }

        if (title is null && description is null && priority is null && dueAt is null)
        {
            return null;
        }

        return new UpdateTicketRequest(title, description, priority, dueAt);
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
            TicketDetailsUpdatedPayload => Icons.Material.Outlined.EditNote,
            _ => Icons.Material.Outlined.Info
        };

    private static string HistoryMarkerClass(AuditEventPayload payload)
        => payload switch
        {
            TicketCreatedPayload => "ticket-history__marker--created",
            TicketStatusChangedPayload => "ticket-history__marker--status",
            TicketAssigneeChangedPayload => "ticket-history__marker--assignee",
            TicketCommentAddedPayload => "ticket-history__marker--comment",
            TicketDetailsUpdatedPayload => "ticket-history__marker--details",
            _ => "ticket-history__marker--default"
        };

    private static string FormatEventType(AuditEventPayload payload)
        => payload switch
        {
            TicketCreatedPayload => "Ticket created",
            TicketStatusChangedPayload => "Status changed",
            TicketAssigneeChangedPayload => "Assignee changed",
            TicketCommentAddedPayload => "Comment added",
            TicketDetailsUpdatedPayload => "Details updated",
            _ => "Audit event"
        };

    private static string FormatEventSummary(AuditEventResponse auditEvent)
        => auditEvent.Payload switch
        {
            TicketCreatedPayload created => $"{created.Title} opened with {FormatPriority(created.Priority)} priority.",
            TicketStatusChangedPayload statusChanged => $"{FormatStatus(statusChanged.FromStatus)} -> {FormatStatus(statusChanged.ToStatus)}.",
            TicketAssigneeChangedPayload assigneeChanged => FormatAssigneeChange(assigneeChanged),
            TicketCommentAddedPayload commentAdded => $"Internal note added by {DisplayActor(commentAdded.Author)}.",
            TicketDetailsUpdatedPayload detailsUpdated => FormatDetailsUpdate(detailsUpdated),
            RawAuditEventPayload => "Raw audit payload captured.",
            _ => "Unknown workflow event."
        };

    private static string FormatDetailsUpdate(TicketDetailsUpdatedPayload payload)
    {
        var changes = new List<string>();

        if (!string.IsNullOrWhiteSpace(payload.NewTitle))
        {
            changes.Add($"title changed to \"{Truncate(payload.NewTitle, 60)}\"");
        }

        if (!string.IsNullOrWhiteSpace(payload.NewDescription))
        {
            changes.Add("description updated");
        }

        if (!string.IsNullOrWhiteSpace(payload.NewPriority))
        {
            changes.Add($"priority changed to {FormatPriority(payload.NewPriority)}");
        }

        if (payload.NewDueAt is not null)
        {
            changes.Add($"due date changed to {FormatDateTime(payload.NewDueAt.Value)}");
        }

        if (changes.Count == 0)
        {
            return "Ticket details updated.";
        }

        var summary = string.Join(", ", changes);
        return char.ToUpperInvariant(summary[0]) + summary[1..] + ".";
    }

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
        History,
        Summary
    }
}