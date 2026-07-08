# ADR 0023: AI Assistant as Edge Adapter (LLM Tool Calling)

## Status

Accepted

## Context and Problem Statement

The application gained an AI intake assistant: users describe an issue in
free text, and a Claude model decides via tool calling whether to create or
update a ticket, with the response streamed live to the browser.

This raises an architectural question: where does an LLM integration live in
a Clean Architecture codebase? The model is an external, non-deterministic
service; its output arrives as a token stream that must reach the browser
with low latency (SSE), and its tool calls are effectively *untrusted input*
that wants to trigger domain state changes.

Three placements were considered:

1. **Application layer port** (`IAssistantService` + Infrastructure
   implementation) — symmetrical to persistence, but forces streaming
   concerns (SSE framing, partial JSON assembly) through an abstraction that
   has exactly one consumer.
2. **Separate service/BFF** — overkill for one endpoint in a showcase.
3. **Edge adapter in the API layer** — the LLM orchestration lives next to
   the endpoint that exposes it; the domain is reached exclusively through
   existing application-layer command handlers.

## Decision Drivers

- **Inward dependency rule stays intact** — Domain and Application must not
  know that an LLM exists.
- **The model must not bypass business rules** — validation, audit trail,
  and outbox staging have to apply to AI-created tickets exactly as to
  API-created ones.
- **Streaming is a presentation concern** — token-by-token delivery, SSE
  event framing, and partial tool-JSON assembly belong to the HTTP edge, not
  to a use case.
- **No premature abstraction** — one consumer does not justify a port
  (same reasoning as ADR 0004's case against MediatR).

## Decision Outcome

The assistant is an **edge adapter** in the API project (`Api/Assistant/`).
The model acts on the system only through the same command handlers the REST
endpoints use.

### What was built

| Layer | Artifact | Purpose |
|---|---|---|
| API | `AssistantChatService` | Agentic loop: streams the model, assembles tool calls, dispatches tools, feeds results back |
| API | `CreateTicketTool`, `UpdateTicketTool` | Tool schema + input parsing/guards + execution through `CreateTicketHandler` / `UpdateTicketHandler` |
| API | `AssistantEndpoints` | `POST /api/v1/assistant/chat`, SSE via `TypedResults.ServerSentEvents` |
| API | `AnthropicOptions` + `AssistantComposition` | API key (user-secrets), model, iteration cap, user timezone; fail-fast validation at startup |
| Contracts | `AssistantChatRequest` | Full conversation transcript (the API is stateless) |
| Application | `UpdateTicketCommand`/`UpdateTicketHandler` | Regular use case — added for the feature, but not assistant-specific |
| Web | `AssistantApiClient`, `AssistantChatPage` | SSE consumer (`SseParser`), live chat UI |

### Key mechanics

- **Non-blocking streaming.** Text deltas are forwarded to the client the
  moment they arrive; tool-use input arrives interleaved as partial JSON
  fragments that are accumulated per content block and parsed at block end.
  One pass over the stream serves both concerns.
- **Tool-calling loop.** On `stop_reason: tool_use` the adapter executes the
  requested tools, appends assistant turn + tool results to the message
  history, and calls the model again — bounded by `MaxToolIterations`.
- **LLM output is untrusted input.** Tool inputs are parsed and guarded
  before touching the domain (schema shape, enum values, due dates in the
  past). Rejected inputs and handler `Result` failures return to the model
  as `tool_result` with `is_error: true`, enabling self-correction inside
  the loop instead of silent bad writes.
- **Stateless with injected time.** The client resends the transcript each
  turn; the current date (with weekday) and the configured user timezone are
  injected into the system prompt per request so relative deadlines resolve
  correctly and due times match the user's local clock.

### What is intentionally missing

- An `IAssistantService` application port — no second consumer exists.
- ~~Conversation persistence — transcripts live in the browser session.~~
  **Superseded by ADR 0026:** conversation state is now persisted server-side and
  the transcript contract has changed (trigger #2 below fired).
- Prompt caching, rate limiting, multi-provider abstraction.
- A generic tool-plugin mechanism — two explicit tool classes are clearer
  at this scale than a registry.

## Consequences

### Positive Consequences

- Domain and Application compile without any Anthropic reference; the LLM
  feature could be deleted by removing one folder, one endpoint group, and
  one DI call.
- AI-created and AI-updated tickets carry the full audit trail (actor
  `ai-assistant`) and outbox staging for free.
- Tool input parsing is static and side-effect free, covered by plain unit
  tests without API key or network.

### Negative Consequences

- The API project now holds non-trivial orchestration logic (~250 lines of
  streaming/loop code) that cannot be reused by a non-HTTP consumer without
  refactoring.
- Each new tool requires a hand-written schema and parser; at a larger tool
  count a declarative mechanism would pay off.
- Resending the full transcript each turn grows request size linearly with
  conversation length (acceptable for a showcase chat).

## Re-evaluation Triggers

Revisit when:

1. A **second consumer** of the assistant appears (CLI, bot, background
   job) — that is the point to introduce an application-layer port.
2. Conversations must be **persisted or resumed** across sessions — state
   moves server-side and the transcript contract changes. **(Fired — see ADR 0026.)**
3. The **tool count grows** beyond a handful — replace explicit tool classes
   with schema generation or a registry.

## Related

- ADR 0004 – Minimal API without MediatR (same anti-abstraction reasoning)
- ADR 0002 – Result pattern (tool failures map onto `Result` errors)
- ADR 0021 – Outbox stub (assistant writes flow through the same staging)
- ADR 0026 – Agent Memory (reverses the stateless/no-persistence stance above)
- `src/ServiceDeskLite.Api/Assistant/AssistantChatService.cs`
- `src/ServiceDeskLite.Api/Assistant/CreateTicketTool.cs`
- `src/ServiceDeskLite.Api/Assistant/UpdateTicketTool.cs`
- `src/ServiceDeskLite.Contracts/V1/Assistant/AssistantChatRequest.cs`
- `docs/operations/runbook.md` – setup and demo flow
