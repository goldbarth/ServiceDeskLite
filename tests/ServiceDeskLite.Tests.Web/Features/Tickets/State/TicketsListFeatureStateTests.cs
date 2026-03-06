using FluentAssertions;

using ServiceDeskLite.Contracts.V1.Common;
using ServiceDeskLite.Contracts.V1.Tickets;
using ServiceDeskLite.Web.Api.V1;
using ServiceDeskLite.Web.Features.Tickets.State;

namespace ServiceDeskLite.Tests.Web.Features.Tickets.State;

public class TicketsListFeatureStateTests
{
    // ── Initial state ───────────────────────────────────────────────────────

    [Fact]
    public void InitialState_Is_Idle()
    {
        var state = new TicketsListFeatureState(FakeApi.ReturnsSuccess());

        state.State.Should().BeOfType<TicketsListState.Idle>();
        state.IsStale.Should().BeFalse();
        state.Query.Should().Be(TicketQueryParams.Default);
    }

    // ── LoadAsync — success ─────────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_OnSuccess_TransitionsToLoaded()
    {
        var page = MakePage(totalCount: 3);
        var sut = new TicketsListFeatureState(FakeApi.ReturnsSuccess(page));

        await sut.LoadAsync(TicketQueryParams.Default);

        var loaded = sut.State.Should().BeOfType<TicketsListState.Loaded>().Subject;
        loaded.Data.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task LoadAsync_StoresQueryBeforeLoading()
    {
        var sut = new TicketsListFeatureState(FakeApi.ReturnsSuccess());
        var query = TicketQueryParams.Default with { Page = 4, PageSize = 10 };

        await sut.LoadAsync(query);

        sut.Query.Should().Be(query);
    }

    [Fact]
    public async Task LoadAsync_ClearsIsStale()
    {
        var sut = new TicketsListFeatureState(FakeApi.ReturnsSuccess());
        sut.Invalidate(); // sets IsStale = true

        await sut.LoadAsync(TicketQueryParams.Default);

        sut.IsStale.Should().BeFalse();
    }

    // ── LoadAsync — failure ─────────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_OnApiFailure_TransitionsToError_WithApiError()
    {
        var apiError = new ApiError { Status = 404, Title = "Not found", Code = "ticket.not_found" };
        var sut = new TicketsListFeatureState(FakeApi.ReturnsFailure(apiError));

        await sut.LoadAsync(TicketQueryParams.Default);

        var error = sut.State.Should().BeOfType<TicketsListState.Error>().Subject;
        error.ApiError.Status.Should().Be(404);
        error.ApiError.Code.Should().Be("ticket.not_found");
    }

    [Fact]
    public async Task LoadAsync_OnUnexpectedException_TransitionsToError_WithStatus500()
    {
        var sut = new TicketsListFeatureState(FakeApi.Throws(new InvalidOperationException("boom")));

        await sut.LoadAsync(TicketQueryParams.Default);

        var error = sut.State.Should().BeOfType<TicketsListState.Error>().Subject;
        error.ApiError.Status.Should().Be(500);
        error.ApiError.Detail.Should().Be("boom");
    }

    // ── LoadAsync — OnChanged notifications ────────────────────────────────

    [Fact]
    public async Task LoadAsync_RaisesOnChanged_ForLoadingAndThenForResult()
    {
        var sut = new TicketsListFeatureState(FakeApi.ReturnsSuccess());
        var statesObserved = new List<TicketsListState>();
        sut.OnChanged += () => statesObserved.Add(sut.State);

        await sut.LoadAsync(TicketQueryParams.Default);

        // First notification: Loading; second: Loaded
        statesObserved.Should().HaveCount(2);
        statesObserved[0].Should().BeOfType<TicketsListState.Loading>();
        statesObserved[1].Should().BeOfType<TicketsListState.Loaded>();
    }

    // ── ReloadAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ReloadAsync_UsesCurrentQuery()
    {
        SearchTicketsRequest? captured = null;
        var sut = new TicketsListFeatureState(FakeApi.CapturingRequest(r => captured = r));
        var query = TicketQueryParams.Default with { Page = 7 };

        await sut.LoadAsync(query);
        captured = null; // reset

        await sut.ReloadAsync();

        captured.Should().NotBeNull();
        captured!.Page.Should().Be(7);
    }

    // ── Invalidate ──────────────────────────────────────────────────────────

    [Fact]
    public void Invalidate_SetsIsStaleTrue()
    {
        var sut = new TicketsListFeatureState(FakeApi.ReturnsSuccess());

        sut.Invalidate();

        sut.IsStale.Should().BeTrue();
    }

    [Fact]
    public void Invalidate_WhenNoSubscribers_DoesNotCallSearchApi()
    {
        var fake = FakeApi.ReturnsSuccess();
        var sut = new TicketsListFeatureState(fake);

        // No subscribers on OnChanged — list page is not mounted.
        sut.Invalidate();

        // LoadAsync was never started, so SearchAsync was never called.
        fake.SearchCallCount.Should().Be(0);
        sut.State.Should().BeOfType<TicketsListState.Idle>();
    }

    [Fact]
    public void Invalidate_WhenSubscribersPresent_TriggersReload_AndClearsIsStale()
    {
        var fake = FakeApi.ReturnsSuccess(); // synchronous — completes before Invalidate() returns
        var sut = new TicketsListFeatureState(fake);
        sut.OnChanged += () => { }; // simulate mounted list page

        sut.Invalidate();

        // Fire-and-forget with synchronous fake: LoadAsync runs to completion
        // before Invalidate() returns, so state is already Loaded.
        sut.State.Should().BeOfType<TicketsListState.Loaded>();
        sut.IsStale.Should().BeFalse();
        fake.SearchCallCount.Should().Be(1);
    }

    // ── Supersession ────────────────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_SecondCallWhileFirstPending_DiscardsFirstResult()
    {
        // First call is blocked on a TCS. Second call returns immediately.
        var firstCallTcs = new TaskCompletionSource<ApiResult<PagedResponse<TicketListItemResponse>>>();
        var callCount = 0;

        var fake = new FakeApi((_, _) =>
        {
            callCount++;
            return callCount == 1
                ? firstCallTcs.Task
                : Task.FromResult(ApiResult<PagedResponse<TicketListItemResponse>>.Success(MakePage(totalCount: 99)));
        });

        var sut = new TicketsListFeatureState(fake);

        // Start first load — it blocks.
        var firstLoad = sut.LoadAsync(TicketQueryParams.Default);
        sut.State.Should().BeOfType<TicketsListState.Loading>();

        // Second load supersedes the first (cancels its CTS, increments seq).
        await sut.LoadAsync(TicketQueryParams.Default with { Page = 2 });
        sut.State.Should().BeOfType<TicketsListState.Loaded>();

        // Unblock the first TCS — its result should be silently discarded by the seq check.
        firstCallTcs.SetResult(ApiResult<PagedResponse<TicketListItemResponse>>.Failure(
            new ApiError { Status = 503, Title = "Should not appear" }));
        await firstLoad;

        // State remains Loaded (from the second call), not overwritten by the first.
        sut.State.Should().BeOfType<TicketsListState.Loaded>();
        var loaded = (TicketsListState.Loaded)sut.State;
        loaded.Data.TotalCount.Should().Be(99);
    }

    // ── Dispose ─────────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_CanBeCalledSafely_WhenNoLoadInFlight()
    {
        var sut = new TicketsListFeatureState(FakeApi.ReturnsSuccess());

        var act = () => sut.Dispose();

        act.Should().NotThrow();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static PagedResponse<TicketListItemResponse> MakePage(int totalCount = 0) =>
        new(Items: [], Page: 1, PageSize: 25, TotalCount: totalCount);

    // ── Fakes ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Minimal in-process fake for ITicketsApiClient.
    /// Only SearchAsync is implemented — all other methods throw NotImplementedException.
    /// </summary>
    private sealed class FakeApi(
        Func<SearchTicketsRequest, CancellationToken, Task<ApiResult<PagedResponse<TicketListItemResponse>>>> searchImpl)
        : ITicketsApiClient
    {
        public int SearchCallCount { get; private set; }

        public Task<ApiResult<PagedResponse<TicketListItemResponse>>> SearchAsync(
            SearchTicketsRequest request, CancellationToken ct = default)
        {
            SearchCallCount++;
            return searchImpl(request, ct);
        }

        // ── Factory helpers ──────────────────────────────────────────────────

        public static FakeApi ReturnsSuccess(PagedResponse<TicketListItemResponse>? page = null)
        {
            var result = ApiResult<PagedResponse<TicketListItemResponse>>.Success(
                page ?? new PagedResponse<TicketListItemResponse>([], 1, 25, 0));
            return new FakeApi((_, _) => Task.FromResult(result));
        }

        public static FakeApi ReturnsFailure(ApiError error)
        {
            var result = ApiResult<PagedResponse<TicketListItemResponse>>.Failure(error);
            return new FakeApi((_, _) => Task.FromResult(result));
        }

        public static FakeApi Throws(Exception ex) =>
            new FakeApi((_, _) => throw ex);

        public static FakeApi CapturingRequest(Action<SearchTicketsRequest> capture)
        {
            var result = ApiResult<PagedResponse<TicketListItemResponse>>.Success(
                new PagedResponse<TicketListItemResponse>([], 1, 25, 0));
            return new FakeApi((req, _) =>
            {
                capture(req);
                return Task.FromResult(result);
            });
        }

        // ── Unused interface members ──────────────────────────────────────────

        public Task<ApiResult<TicketResponse>> GetByIdAsync(Guid id, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<ApiResult<CreateTicketResponse>> CreateAsync(CreateTicketRequest request, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<ApiResult<TicketResponse>> ChangeStatusAsync(Guid id, ChangeTicketStatusRequest request, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<ApiResult<TicketResponse>> AssignAsync(Guid id, AssignTicketRequest request, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<ApiResult<CommentResponse>> AddCommentAsync(Guid id, AddCommentRequest request, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<ApiResult<IReadOnlyList<AuditEventResponse>>> GetAuditEventsAsync(Guid id, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
