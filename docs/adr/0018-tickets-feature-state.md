# ADR 0018: Tickets Feature State — Scoped Service with URL Sync and Invalidation

## Status

Accepted

## Context and Problem Statement

The Tickets list page manages four concerns inline in its code-behind:

1. **Query state** — page, pageSize, sort field, sort direction stored as separate mutable fields.
2. **Load effects** — `SearchAsync` calls with cancellation and sequencing to prevent stale results.
3. **URL synchronisation** — none existed; refreshing the page or sharing a URL lost all filter/sort state.
4. **Invalidation** — after mutations on the Detail page (ChangeStatus, Assign, AddComment), the list
   had no standard mechanism to signal that its data was outdated.

Scatter of these concerns across the page component made the code hard to test, led to potential
illegal states (e.g. `_isLoading = true` and `_apiError != null` simultaneously), and prevented
navigation-safe state preservation.

## Decision Drivers

- Query state and load effects must be testable without Blazor rendering infrastructure.
- Filter/paging/sort state must survive a page refresh (`/tickets?page=3&sort=Priority&dir=Asc`).
- Mutations on the Detail page must trigger a deterministic list refresh without direct coupling.
- No new global architecture: the solution must remain scoped to the Tickets feature.

## Considered Options

### Option A — Inline fields + URL sync only (minimal change)

Keep fields in the page component and add `NavigationManager`-based URL sync. Mutations trigger
a reload via direct page-to-page coupling (e.g. a static event or passed callback).

**Rejected:** Direct coupling between Detail and List pages violates encapsulation. Inline fields
still make testing require full Blazor rendering. Illegal states remain possible.

### Option B — Scoped Feature State service + URL sync (Selected)

Introduce `TicketsListFeatureState` as a Scoped DI service. It owns the state machine, the
CancellationToken management, and the sequencing guard. The page component is a thin subscriber:
it reads from the service and delegates all actions to it. URL sync is handled in the page, which
reads query parameters on mount and writes them after each load.

### Option C — Fluxor or Redux-style global state

A full reactive state management library (Fluxor, BlazorState) would handle all the above
and add DevTools support, action replay, and middleware. Rejected as over-engineering: the
Tickets feature has a single list and a single detail — a full state management library would
add significant boilerplate with no functional benefit for this scope.

## Decision Outcome

Chosen option: **Option B**.

## Architecture

### New types

```
Features/Tickets/
├── State/
│   ├── TicketQueryParams          — Immutable record: page, pageSize, sort
│   ├── TicketsListState           — Discriminated union: Idle | Loading | Loaded | Error
│   └── TicketsListFeatureState    — Scoped service: state machine, effects, invalidation
└── TicketsFeatureComposition      — AddTicketsFeature() DI extension
```

### `TicketQueryParams` — immutable value object

All query parameters are grouped into a single `record`. Transitions use `with` expressions,
making every state change explicit and traceable. Value equality (built into records) enables
cheap change detection before triggering loads or URL updates.

```csharp
// Toggle sort without touching individual fields:
var next = Query.WithSort(TicketSortField.Priority); // new record, old record unchanged
```

### `TicketsListState` — discriminated union

```
Idle  →  Loading  →  Loaded(Data)
                  →  Error(ApiError)
                  ↑
             ReloadAsync / Invalidate
```

Illegal states are impossible by construction: `_isLoading` and `_apiError` can no longer
coexist. `EmptyState` was removed — zero items is a rendering concern, not a state concern.
Unexpected exceptions are converted to a synthetic `ApiError(Status=500)` before reaching
the union, so the UI always handles exactly one error type.

### `TicketsListFeatureState` — Scoped service

- **Lifetime:** Scoped = one instance per SignalR circuit (browser tab). State survives
  navigation within the session; it is discarded when the circuit disconnects.
- **CancellationToken management:** each `LoadAsync` call cancels the previous in-flight
  `CancellationTokenSource`, ensuring only one request is active at a time.
- **Sequencing guard:** a monotonically increasing `_seq` counter discards results from
  superseded loads even if cancellation did not propagate through the HTTP layer.
- **`OnChanged` event:** raised on every state transition. The page subscribes on mount
  and unsubscribes on dispose. `InvokeAsync(StateHasChanged)` marshals back to the
  Blazor renderer thread.

### URL synchronisation

`TicketsListPage` reads `NavigationManager.Uri` on `OnInitializedAsync` to reconstruct
`TicketQueryParams` from the query string. After each load it writes only non-default
parameters back, keeping URLs clean:

```
/tickets                                    — all defaults (page 1, pageSize 25, sort CreatedAt desc)
/tickets?page=3                             — page 3, rest default
/tickets?sort=Priority&dir=Asc&pageSize=50  — custom sort and page size
```

`replace: true` in `NavigateTo` replaces the current history entry so that pagination
changes do not pollute the browser back-stack.

### Invalidation

Mutations on the Detail page (ChangeStatus, Assign, AddComment) call `Invalidate()`:

```csharp
// Two paths depending on whether the list page is currently mounted:
public void Invalidate()
{
    _isStale = true;
    if (OnChanged is not null)
        _ = ReloadAsync(); // list is mounted — reload immediately (fire-and-forget, safe)
    // else: stale flag is checked on next OnInitializedAsync
}
```

The list page checks `IsStale` on mount:

```csharp
if (FeatureState is { State: TicketsListState.Loaded, IsStale: false })
    return; // fresh — skip reload
```

This mirrors the stale-while-revalidate pattern used by TanStack Query and SWR, implemented
with .NET primitives and without an external library.

## Consequences

### Positive

- `TicketsListFeatureState` is a plain C# class testable with a fake `ITicketsApiClient`
  — no Blazor TestContext or WebApplicationFactory needed.
- Filter/paging/sort state is reproducible via URL: bookmarks and browser refresh work correctly.
- Mutations on the Detail page trigger a deterministic refresh with a single call to `Invalidate()`.
- The page component is reduced to a thin subscriber with no async logic of its own.
- New filter criteria (text search, status filter) can be added to `TicketQueryParams` and
  `ParseQueryFromUrl` / `SyncUrl` without touching the state machine or the load logic.

### Negative

- The Scoped lifetime means the list state is shared across all components rendered within
  the same circuit. If a second component on the same page also renders a ticket list,
  it would observe the same state (acceptable for the current single-list layout).
- `OnChanged` is a plain `event Action` — there is no built-in mechanism to prevent a
  component from forgetting to unsubscribe, which would cause a memory leak. The pattern
  requires `IDisposable` on every subscriber (enforced by convention, not the compiler).

## Re-evaluation Triggers

Revisit when:

1. The application adds more features with similar list/detail/mutation patterns — extract
   a generic `FeatureListState<TQuery, TItem>` base or adopt a reactive state library.
2. Multiple components on the same page need independent list states — consider Transient
   state or component-scoped state instead of Scoped.

## Related

- ADR 0015 – Blazor Interactive Server + MudBlazor (Scoped lifetime explanation)
- ADR 0002 – Result pattern (ApiResult used in state transitions)
- `src/ServiceDeskLite.Web/Features/Tickets/State/`
- `src/ServiceDeskLite.Web/Features/Tickets/Pages/TicketsListPage.razor.cs`
- `tests/ServiceDeskLite.Tests.Web/Features/Tickets/State/`
