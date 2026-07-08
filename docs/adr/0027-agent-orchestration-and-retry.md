# ADR 0027: Autonomous Multi-Tool Orchestration and Transient-Fault Retry

## Status

Accepted

## Context and Problem Statement

The assistant's agentic loop (`AssistantChatService`, ADR 0023) already supports
multiple tools per turn and iterates up to `MaxToolIterations`, so a chain like
`find_similar_tickets` → `create_ticket` → `assign_ticket` is mechanically
possible. Two things stopped it from being useful autonomy:

1. **The model tended to stop after one step** — it treated each tool as the end
   of the turn rather than planning a sequence.
2. **Transient tool failures broke the chain.** Deterministic failures
   (validation, workflow conflicts) already return `tool_result is_error:true`
   and the model self-corrects — that path works. But a transient fault had no
   handling: in #153 a Voyage `429 Too Many Requests` surfaced to the model as a
   `recall_memory` "technical error" and derailed the step, even though a simple
   retry would have succeeded.

The fix must not weaken the core invariant: tools act **only** through the
application-layer command handlers; nothing may reach the domain directly.

## Decision Drivers

- **Autonomy without new machinery** — the loop is already a planner; the model
  is the orchestrator. Prefer guiding it over building a separate planning stage.
- **Resilience without hiding real errors** — retry the transient, surface the
  deterministic. Never retry a validation failure; never silently swallow a
  permanent one.
- **Testable in isolation** — retry logic must be unit-testable without the
  Anthropic client or a network.
- **No new dependency** for a small, well-understood retry need.

## Decision Outcome

### Orchestration is prompt-driven

The system prompt instructs the model to carry out an implied multi-step sequence
in one turn — check duplicates, then create, then assign — using each tool result
to decide the next step, and to pause for the user only when a decision is
genuinely theirs or a step fails in a way only they can resolve. No planner
object, no orchestration state machine: the loop plus the prompt is the planner.
`MaxToolIterations` default was raised `4 → 6` to give a full chain headroom for a
correction/retry round.

### Transient retry lives in the edge loop

A small, dependency-free policy sits between the loop and each tool call:

- **`TransientFault.IsTransient(ex, callerToken)`** classifies conservatively:
  `HttpRequestException` with 408/429/500/502/503/504, `TimeoutException`, and a
  cancellation whose token is **not** the caller's (an operation timeout, not a
  client disconnect). Anything unrecognized is permanent.
- **`ToolRetryPolicy.ExecuteAsync(action, maxRetries, backoff, logger, ct)`**
  retries only transient exceptions with bounded exponential backoff; on
  exhaustion — or any non-transient exception — it converts the failure into an
  `is_error` tool result so the model always receives a `tool_result` and the
  stream never breaks. A caller cancellation always propagates.
- The three embedding-backed tools (`find_similar_tickets`, `remember`,
  `recall_memory`) stop swallowing transient exceptions and let them propagate to
  the policy; their non-transient "technical error" and "unavailable" paths are
  unchanged.

Because the policy re-invokes the same `ExecuteToolAsync` path, a retry still
flows through the application-layer handlers — the invariant holds.

Configuration (`AnthropicOptions`, validated at startup): `MaxToolRetries`
(default 2, 0–5), `ToolRetryBaseDelayMs` (default 200, 0–5000).

### Why edge-loop retry, not HTTP/Polly

An HTTP-layer resilience handler (e.g. Polly via `Microsoft.Extensions.Http.Resilience`)
would only cover Voyage HTTP calls and would add a dependency + its own ADR. The
edge-loop policy is general (covers any transient tool exception, including
future DB transients), needs no new package, and lives where "retry between
orchestration steps" conceptually belongs.

## Consequences

### Positive

- A single request can trigger a validated multi-tool chain in one turn; a
  transient blip mid-chain is retried and recovers instead of aborting the step.
- Retry is unit-tested (`ToolRetryPolicyTests`, `TransientFaultTests`) with zero
  backoff and no network; the chain's application-layer effect is covered E2E on
  both providers (`AssistantOrchestrationTests`).
- No new dependency; no change to the handler invariant.

### Negative

- Retries add bounded, silent latency to the SSE stream mid-chain.
- Transient classification is heuristic; an unclassified transient fault surfaces
  once as `is_error` (the model then reacts) rather than being retried.
- Autonomy is only as good as the model's planning; the prompt nudges but does not
  guarantee a particular sequence.

## Re-evaluation Triggers

Revisit when:

1. Tool chains routinely exceed the iteration cap — make `MaxToolIterations`
   deployment-tuned or add step budgeting.
2. Transient faults appear from sources the heuristic misses — centralize
   classification or adopt a resilience library.
3. A second transport (beyond Voyage HTTP) needs retry — reconsider an HTTP-layer
   handler alongside the loop policy.

## Related

- ADR 0023 – AI Assistant as Edge Adapter (the loop this extends)
- ADR 0026 – Agent Memory (the `remember`/`recall_memory` tools that surfaced the 429)
- ADR 0002 – Result pattern (deterministic failures as values, not exceptions)
- `src/ServiceDeskLite.Api/Assistant/ToolRetryPolicy.cs`, `TransientFault.cs`
- `src/ServiceDeskLite.Api/Assistant/AssistantChatService.prompt.cs` (orchestration guidance)
