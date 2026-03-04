# ADR 0015: Blazor Interactive Server + MudBlazor as the Web UI Stack

## Status

Accepted

## Context and Problem Statement

The Web frontend needs a rendering strategy and a component library. Blazor
offers three rendering modes with different trade-offs on interactivity,
initial load, and infrastructure requirements. A component library on top
of Blazor determines the visual language, the available UI primitives, and
how much custom CSS is needed.

A second, architectural question is whether the Web should call the API over
HTTP or bypass it by referencing the Application handlers directly — which
would be possible since they share the same process in development.

## Decision Drivers

- UI components must be interactive (forms, tables with sorting and paging,
  real-time state updates) without requiring a full page reload.
- The Web project must not reference `Application`, `Domain`, or
  `Infrastructure` layers — the API is the only entry point for data.
- The component library must provide production-ready, accessible components
  (data tables, dialogs, snackbars, forms) without requiring a large amount
  of custom CSS.
- The rendering model must be simple to reason about in the context of a
  showcase project with a single server.

## Considered Options

### Option A — Blazor Interactive Server + MudBlazor (Selected)

All component rendering runs on the server over a persistent SignalR
connection. UI events (button clicks, sort changes, pagination) are sent
to the server, handled in C#, and the resulting DOM diff is pushed back to
the browser. MudBlazor provides the component library (Material Design,
Blazor-native, no JavaScript wrappers). The Web project communicates with
the API exclusively over HTTP via `ITicketsApiClient`.

### Option B — Blazor WebAssembly + MudBlazor

.NET runs in the browser via WebAssembly. No persistent server connection
required after the initial load. Suitable for offline-capable apps or CDN
deployment. However, the initial WASM download is larger, cold-start latency
is higher, and the Web project would need its own API-access strategy
(CORS, auth tokens). Adds complexity that is not justified for a
server-hosted showcase.

### Option C — Blazor Static SSR

The server renders full HTML pages with no persistent connection. Interactivity
requires JavaScript or form post/redirect cycles. Appropriate for
content-heavy sites with minimal client-side state, but incompatible with
the interactive list, sort, and pagination UX this project requires without
significant JavaScript.

## Decision Outcome

Chosen option: **Option A — Blazor Interactive Server + MudBlazor**.

### Why this trade-off makes sense for this project

- **Interactive Server keeps the stack homogeneous.** All logic — UI event
  handling, API calls, state management — is written in C# on the server.
  No TypeScript, no separate frontend build pipeline, no CORS setup.
- **SignalR overhead is acceptable for a single-server showcase.** The
  persistent connection model of Interactive Server becomes a scalability
  concern with many concurrent users and sticky sessions. For a reference
  implementation with a single host, this trade-off is explicitly accepted.
- **MudBlazor is Blazor-native, not a JavaScript wrapper.** Components
  are implemented in C# and Razor without wrapping a JavaScript library.
  This means full server-side rendering compatibility and no JavaScript
  interop surprises. The data table (`MudDataGrid`, `MudTable`), dialogs,
  and snackbars cover the required UI primitives without custom CSS work.
- **The Web calls the API over HTTP — no shortcut through shared handlers.**
  `ITicketsApiClient` / `TicketsApiClient` make HTTP calls to the API.
  The Web project does not reference `Application`, `Domain`, or
  `Infrastructure`. This enforces the deployment boundary: the API and Web
  can be hosted separately, and the Web's only contract with the backend is
  the HTTP API surface defined in `Contracts`.

### Key registration

```csharp
// Rendering mode
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// HTTP client — typed, configured from ApiClient:BaseUrl
services.AddHttpClient<ITicketsApiClient, TicketsApiClient>(...);
```

## Consequences

### Positive Consequences

- Interactive UI without any JavaScript or frontend build tooling.
- Full C# for event handling, validation, and API calls.
- MudBlazor components reduce the CSS surface to near zero for standard
  UI patterns.
- The HTTP boundary between Web and API is real — integration tests
  and the API client can be tested independently of the Blazor rendering.

### Negative Consequences

- Every connected browser holds a server-side SignalR circuit. Session
  affinity (sticky sessions) is required if the API is load-balanced
  across multiple instances.
- Connection loss (network interruption, server restart) drops the UI state.
  The user must reload the page to reconnect.
- Interactive Server does not support pre-rendering to static HTML for
  SEO or fast first-paint — the page is blank until the SignalR connection
  is established.

## Re-evaluation Triggers

Revisit when:

1. The application needs to run offline or be deployed as a CDN-hosted
   SPA — candidate: Blazor WebAssembly or a JavaScript framework.
2. Load-balancing or horizontal scaling is introduced — evaluate whether
   sticky sessions are acceptable or whether a move to Blazor WebAssembly
   or a stateless rendering model is warranted.

## Related

- ADR 0010 – Versioned HTTP Contracts in a dedicated `Contracts` project
- `src/ServiceDeskLite.Web/Program.cs`
- `src/ServiceDeskLite.Web/Composition/ApiClientComposition.cs`
- `src/ServiceDeskLite.Web/Api/V1/ITicketsApiClient.cs`
- `src/ServiceDeskLite.Web/Api/V1/TicketsApiClient.cs`
