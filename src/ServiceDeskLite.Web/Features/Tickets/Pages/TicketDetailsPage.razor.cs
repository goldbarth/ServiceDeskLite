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

    // Injected to invalidate the list after any mutation on this page.
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

    // -----------------------------------------------------------------------
    // Lifecycle
    // -----------------------------------------------------------------------

    protected override async Task OnParametersSetAsync()
    {
        _isLoading = true;
        _error = null;
        _ticket = null;
        _auditEvents = null;
        _auditEventsError = null;

        var ticketResult = await TicketsApi.GetByIdAsync(Id);
        var auditResult  = await TicketsApi.GetAuditEventsAsync(Id);

        if (ticketResult.IsSuccess)
            _ticket = ticketResult.Value;
        else
            _error = ticketResult.Error;

        if (auditResult.IsSuccess)
            _auditEvents = auditResult.Value;
        else
            _auditEventsError = auditResult.Error;

        _isLoading = false;
    }

    // -----------------------------------------------------------------------
    // Mutations — each calls Invalidate() so the list refreshes on next visit
    // -----------------------------------------------------------------------

    private async Task OpenChangeStatusDialogAsync()
    {
        var parameters = new DialogParameters<ChangeStatusDialog>
        {
            { x => x.TicketId, _ticket!.Id },
            { x => x.AllowedStatuses, _ticket!.AllowedTransitions }
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
            { x => x.CurrentAssignee, _ticket!.Assignee }
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
            return;

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
            var updated = await TicketsApi.GetByIdAsync(_ticket!.Id);
            if (updated.IsSuccess)
                _ticket = updated.Value;

            TicketsListState.Invalidate();
            await RefreshAuditEventsAsync();
            _commentContent = null;
            _commentAuthor = null;
            Snackbar.Add("Comment added.", Severity.Success);
            return;
        }

        _commentError = result.Error;
    }

    private async Task RefreshAuditEventsAsync()
    {
        var r = await TicketsApi.GetAuditEventsAsync(Id);
        if (r.IsSuccess)
            _auditEvents = r.Value;
    }

    // -----------------------------------------------------------------------
    // History rendering helpers
    // -----------------------------------------------------------------------

    private static Color EventTypeColor(AuditEventPayload payload) => payload switch
    {
        TicketCreatedPayload         => Color.Success,
        TicketStatusChangedPayload   => Color.Info,
        TicketAssigneeChangedPayload => Color.Warning,
        TicketCommentAddedPayload    => Color.Default,
        _                            => Color.Default
    };

    private static string FormatEventType(AuditEventPayload payload) => payload switch
    {
        TicketCreatedPayload         => "Ticket created",
        TicketStatusChangedPayload   => "Status changed",
        TicketAssigneeChangedPayload => "Assignee changed",
        TicketCommentAddedPayload    => "Comment added",
        _                            => "Unknown event"
    };
}
