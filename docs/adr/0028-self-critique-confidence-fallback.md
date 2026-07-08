# ADR 0028: Self-Critique, Confidence Signal, and Tool Fallback

## Status

Accepted

## Context and Problem Statement

The agent could already self-correct on deterministic failures (`is_error` results, ADR 0023) and
retry transient ones (ADR 0027). What it lacked was a self-review layer within a turn:

1. **No confidence signal** on tool outcomes. A strong semantic match and a weak one looked the same
   to any downstream consumer, and nothing was captured for the planned dashboard/observability work.
2. **No fallback when a retrieval path fails.** If semantic search was unavailable (InMemory, or no
   Voyage key) or returned nothing, `find_similar_tickets` reported "unavailable" and the duplicate
   check dead-ended.
3. **A loose correction loop.** The prompt did not tell the model to distrust weak, empty, or
   contradictory results and re-plan.

## Decision Drivers

- **Capture, do not just display.** A confidence signal must be structurally available (log + event)
  for future observability, not only embedded in the model-facing text.
- **Graceful, honest degradation.** A failed primary path should fall back to a weaker path that is
  clearly labelled, not silently faked and not a dead end.
- **No handler bypass.** Any fallback must still run through the application layer.
- **Deterministically testable.** The correction/fallback path must be coverable without a live model.

## Decision Outcome

### Confidence signal

The tool result gains a fourth field, `double? Confidence`, alongside the existing optional
`Guid? TicketId`. Retrieval tools (`find_similar_tickets`, `recall_memory`) set it to the strongest
match's similarity (`0.0` when there are none); all other tools return `null` (not applicable). The
streaming loop logs it and emits it on the `tool_result` SSE event (`AssistantSseEvent.Confidence`,
mirrored on the Web `AssistantStreamEvent`). This is the signal the future dashboard consumes.

### Keyword fallback for `find_similar_tickets`

When the semantic search is unavailable or returns no matches, the tool falls back to keyword search
through the existing `SearchTicketsHandler` (the same handler behind `search_tickets`), and formats the
hits with an explicit label ("Semantic search is unavailable here." / "No semantic matches found." +
"Keyword-matched N ticket(s) ... keyword hits are less precise than semantic ones"). Keyword hits carry
`null` confidence - there is no vector score to report and none is invented.

This **refines the honest-degradation stance** of ADR 0024/0026: instead of reporting "unavailable" on
InMemory or without a Voyage key, the tool now returns labelled keyword matches. It is still honest (the
result is clearly marked as keyword-based, not semantic) and no longer a dead end. The fallback reuses an
existing application handler, so it works on both persistence providers and never bypasses the domain.

### Prompt-driven self-critique

The system prompt now tells the model to critique each tool result before acting: if it is weak (low
similarity), empty, or contradicts the request, re-plan in the same turn (refine the query, try another
tool, or ask the user) rather than asserting a false duplicate; and to treat keyword-fallback hits as
weaker evidence than semantic ones.

## Consequences

### Positive

- The confidence signal is captured structurally (log + SSE) for later observability.
- `find_similar_tickets` is useful on every deployment, degrading to labelled keyword search instead of
  dead-ending; the fallback is deterministically tested on real InMemory services.
- No new dependency, no new port, no handler bypass.

### Negative

- Behavior change: `find_similar_tickets` no longer returns "unavailable" on InMemory / without Voyage.
  Documented here; the model is told keyword hits are weaker.
- The confidence field adds a fourth element to the tool-result tuple across all tools (mechanical, but
  consistent with the existing optional `TicketId`).

## Re-evaluation Triggers

Revisit when:

1. The dashboard/observability work lands - the confidence signal may need a richer shape (per-source
   scores, calibration) than a single top-similarity number.
2. More tools need fallbacks - a shared fallback abstraction may beat per-tool wiring.
3. Confidence starts driving control flow (auto-reject below a threshold) rather than informing the model.

## Related

- ADR 0023 - AI Assistant as Edge Adapter (the loop and `is_error` self-correction)
- ADR 0024 - Semantic Ticket Search (the honest-degradation stance this refines)
- ADR 0026 - Agent Memory (`recall_memory`, also now confidence-scored)
- ADR 0027 - Orchestration and transient retry (the prior self-correction layer)
- `src/ServiceDeskLite.Api/Assistant/FindSimilarTicketsTool.cs`
- `src/ServiceDeskLite.Api/Assistant/AssistantChatService.prompt.cs`
