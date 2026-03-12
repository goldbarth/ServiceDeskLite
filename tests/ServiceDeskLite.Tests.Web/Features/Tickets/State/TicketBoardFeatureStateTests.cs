using FluentAssertions;

using ServiceDeskLite.Contracts.V1.Common;
using ServiceDeskLite.Contracts.V1.Dashboard;
using ServiceDeskLite.Contracts.V1.Tickets;
using ServiceDeskLite.Web.Api.V1;
using ServiceDeskLite.Web.Features.Tickets.State;

namespace ServiceDeskLite.Tests.Web.Features.Tickets.State;

public sealed class TicketBoardFeatureStateTests
{
    // ── Initial state ────────────────────────────────────────────────────────

    [Fact]
    public void InitialState_IsIdle_AndHideClosedIsFalse()
    {
        var sut = new TicketBoardFeatureState(FakeApi.SearchReturnsSuccess());

        sut.State.Should().BeOfType<TicketBoardState.Idle>();
        sut.HideClosed.Should().BeFalse();
    }

    // ── LoadAsync — success ──────────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_OnSuccess_TransitionsToLoaded_WithTickets()
    {
        var tickets = new[] { MakeTicket(TicketStatus.New), MakeTicket(TicketStatus.Triaged) };
        var sut = new TicketBoardFeatureState(FakeApi.SearchReturnsSuccess(tickets));

        await sut.LoadAsync();

        var loaded = sut.State.Should().BeOfType<TicketBoardState.Loaded>().Subject;
        loaded.Tickets.Should().HaveCount(2);
    }

    // ── LoadAsync — failure ──────────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_OnApiFailure_TransitionsToError()
    {
        var apiError = new ApiError { Status = 503, Title = "Service unavailable" };
        var sut = new TicketBoardFeatureState(FakeApi.SearchReturnsFailure(apiError));

        await sut.LoadAsync();

        var error = sut.State.Should().BeOfType<TicketBoardState.Error>().Subject;
        error.ApiError.Status.Should().Be(503);
    }

    [Fact]
    public async Task LoadAsync_OnUnexpectedException_TransitionsToError_WithStatus500()
    {
        var sut = new TicketBoardFeatureState(FakeApi.SearchThrows(new InvalidOperationException("boom")));

        await sut.LoadAsync();

        var error = sut.State.Should().BeOfType<TicketBoardState.Error>().Subject;
        error.ApiError.Status.Should().Be(500);
        error.ApiError.Detail.Should().Be("boom");
    }

    // ── LoadAsync — OnChanged notifications ──────────────────────────────────

    [Fact]
    public async Task LoadAsync_RaisesOnChanged_ForLoadingAndThenForResult()
    {
        var sut = new TicketBoardFeatureState(FakeApi.SearchReturnsSuccess());
        var statesObserved = new List<TicketBoardState>();
        sut.OnChanged += () => statesObserved.Add(sut.State);

        await sut.LoadAsync();

        // First notification: Loading, second: Loaded
        statesObserved.Should().HaveCount(2);
        statesObserved[0].Should().BeOfType<TicketBoardState.Loading>();
        statesObserved[1].Should().BeOfType<TicketBoardState.Loaded>();
    }

    // ── MoveAsync — success ──────────────────────────────────────────────────

    [Fact]
    public async Task MoveAsync_OnSuccess_ReturnsNull()
    {
        var sut = new TicketBoardFeatureState(FakeApi.AllReturnsSuccess());

        var error = await sut.MoveAsync(Guid.NewGuid(), TicketStatus.Triaged);

        error.Should().BeNull();
    }

    [Fact]
    public async Task MoveAsync_OnSuccess_TriggersReload()
    {
        var fake = FakeApi.AllReturnsSuccess();
        var sut = new TicketBoardFeatureState(fake);

        await sut.MoveAsync(Guid.NewGuid(), TicketStatus.InProgress);

        fake.SearchCallCount.Should().Be(1);
        sut.State.Should().BeOfType<TicketBoardState.Loaded>();
    }

    // ── MoveAsync — failure ──────────────────────────────────────────────────

    [Fact]
    public async Task MoveAsync_OnApiFailure_ReturnsError()
    {
        var moveError = new ApiError { Status = 400, Code = "domain.ticket.status.invalid_transition" };
        var fake = FakeApi.AllReturnsSuccess(changeStatusError: moveError);
        var sut = new TicketBoardFeatureState(fake);

        var error = await sut.MoveAsync(Guid.NewGuid(), TicketStatus.Closed);

        error.Should().NotBeNull();
        error!.Code.Should().Be("domain.ticket.status.invalid_transition");
    }

    [Fact]
    public async Task MoveAsync_OnApiFailure_DoesNotReload()
    {
        var moveError = new ApiError { Status = 400 };
        var fake = FakeApi.AllReturnsSuccess(changeStatusError: moveError);
        var sut = new TicketBoardFeatureState(fake);

        await sut.MoveAsync(Guid.NewGuid(), TicketStatus.Closed);

        // ChangeStatusAsync failed — LoadAsync must never have been called.
        fake.SearchCallCount.Should().Be(0);
    }

    // ── ToggleHideClosed ─────────────────────────────────────────────────────

    [Fact]
    public void ToggleHideClosed_InitiallyFalse_BecomesTrue()
    {
        var sut = new TicketBoardFeatureState(FakeApi.SearchReturnsSuccess());

        sut.ToggleHideClosed();

        sut.HideClosed.Should().BeTrue();
    }

    [Fact]
    public void ToggleHideClosed_ToggledTwice_ReturnsFalse()
    {
        var sut = new TicketBoardFeatureState(FakeApi.SearchReturnsSuccess());

        sut.ToggleHideClosed();
        sut.ToggleHideClosed();

        sut.HideClosed.Should().BeFalse();
    }

    [Fact]
    public void ToggleHideClosed_RaisesOnChanged()
    {
        var sut = new TicketBoardFeatureState(FakeApi.SearchReturnsSuccess());
        var raised = false;
        sut.OnChanged += () => raised = true;

        sut.ToggleHideClosed();

        raised.Should().BeTrue();
    }

    [Fact]
    public void ToggleHideClosed_DoesNotTriggerApiCall()
    {
        var fake = FakeApi.SearchReturnsSuccess();
        var sut = new TicketBoardFeatureState(fake);

        sut.ToggleHideClosed();

        // Toggling is a pure client-side display filter — no server round-trip.
        fake.SearchCallCount.Should().Be(0);
    }

    // ── Dispose ──────────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_CanBeCalledSafely_WhenNoLoadInFlight()
    {
        var sut = new TicketBoardFeatureState(FakeApi.SearchReturnsSuccess());

        var act = () => sut.Dispose();

        act.Should().NotThrow();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static TicketListItemResponse MakeTicket(TicketStatus status = TicketStatus.New) =>
        new(Guid.NewGuid(), "Test ticket", TicketPriority.Low, status, DateTimeOffset.UtcNow, null, null, [], false, "#ABC1234");

    // ── Fakes ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Minimal in-process fake for ITicketsApiClient.
    /// Delegates SearchAsync and ChangeStatusAsync via injected Funcs.
    /// All other methods throw NotImplementedException.
    /// </summary>
    private sealed class FakeApi(
        Func<SearchTicketsRequest, CancellationToken, Task<ApiResult<PagedResponse<TicketListItemResponse>>>> searchImpl,
        Func<Guid, ChangeTicketStatusRequest, CancellationToken, Task<ApiResult<TicketResponse>>> changeStatusImpl)
        : ITicketsApiClient
    {
        public int SearchCallCount { get; private set; }
        public int ChangeStatusCallCount { get; private set; }

        // ── Factory helpers ───────────────────────────────────────────────────

        /// <summary>SearchAsync succeeds; ChangeStatusAsync is not expected to be called.</summary>
        public static FakeApi SearchReturnsSuccess(IEnumerable<TicketListItemResponse>? items = null)
        {
            var page = new PagedResponse<TicketListItemResponse>(
                Items: items?.ToList() ?? [], Page: 1, PageSize: 200, TotalCount: 0);
            return new(
                (_, _) => Task.FromResult(ApiResult<PagedResponse<TicketListItemResponse>>.Success(page)),
                (_, _, _) => throw new NotImplementedException());
        }

        public static FakeApi SearchReturnsFailure(ApiError error)
        {
            var result = ApiResult<PagedResponse<TicketListItemResponse>>.Failure(error);
            return new(
                (_, _) => Task.FromResult(result),
                (_, _, _) => throw new NotImplementedException());
        }

        public static FakeApi SearchThrows(Exception ex) =>
            new((_, _) => throw ex, (_, _, _) => throw new NotImplementedException());

        /// <summary>
        /// Both SearchAsync and ChangeStatusAsync are configured.
        /// Pass <paramref name="changeStatusError"/> to simulate a rejected transition.
        /// </summary>
        public static FakeApi AllReturnsSuccess(ApiError? changeStatusError = null)
        {
            var searchResult = ApiResult<PagedResponse<TicketListItemResponse>>.Success(
                new PagedResponse<TicketListItemResponse>([], 1, 200, 0));

            var changeStatusResult = changeStatusError is null
                ? ApiResult<TicketResponse>.Success(
                    new TicketResponse(
                        Guid.NewGuid(),
                        "T",
                        "D",
                        TicketPriority.Low,
                        TicketStatus.New,
                        DateTimeOffset.UtcNow,
                        null,
                        null,
                        [],
                        [TicketStatus.Triaged],
                        IsOverdue: false,
                        DisplayRef: "#ABC1234",
                        StatusGuidance: string.Empty,
                        SuggestedNextSteps: []))
                : ApiResult<TicketResponse>.Failure(changeStatusError);

            return new(
                (_, _) => Task.FromResult(searchResult),
                (_, _, _) => Task.FromResult(changeStatusResult));
        }

        // ── Interface implementation ──────────────────────────────────────────

        public Task<ApiResult<PagedResponse<TicketListItemResponse>>> SearchAsync(
            SearchTicketsRequest request, CancellationToken ct = default)
        {
            SearchCallCount++;
            return searchImpl(request, ct);
        }

        public Task<ApiResult<TicketResponse>> ChangeStatusAsync(
            Guid id, ChangeTicketStatusRequest request, CancellationToken ct = default)
        {
            ChangeStatusCallCount++;
            return changeStatusImpl(id, request, ct);
        }

        // ── Unused interface members ──────────────────────────────────────────

        public Task<ApiResult<TicketResponse>> GetByIdAsync(Guid id, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<ApiResult<CreateTicketResponse>> CreateAsync(CreateTicketRequest request, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<ApiResult<TicketResponse>> AssignAsync(Guid id, AssignTicketRequest request, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<ApiResult<CommentResponse>> AddCommentAsync(Guid id, AddCommentRequest request, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<ApiResult<IReadOnlyList<AuditEventResponse>>> GetAuditEventsAsync(Guid id, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<ApiResult<DashboardSummaryResponse>> GetDashboardSummaryAsync(CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
