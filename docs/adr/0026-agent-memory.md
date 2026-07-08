# ADR 0026: Agent Memory — Server-Side Conversation State and Long-Term Recall

## Status

Accepted (supersedes the "stateless / no conversation persistence" stance of ADR 0023)

## Context and Problem Statement

ADR 0023 built the assistant as a **stateless** edge adapter: the browser
resent the full transcript on every turn, and conversations were deliberately
not persisted. Its re-evaluation trigger #2 named exactly the change now
required — "conversations must be persisted or resumed → state moves server-side
and the transcript contract changes."

The agent-memory work (roadmap Phase 1) asks for two capabilities:

1. **Short-term:** conversation state survives across requests without the
   client resending the whole transcript.
2. **Long-term:** durable facts about the user (preferences, profile) are
   embedded and recalled by semantic relevance across separate conversations,
   exposed to the model as its own tool.

Both need persistence that works on **both** providers (Postgres + InMemory),
must not leak LLM types into the inner layers, and must degrade honestly where
an embedding provider is absent.

## Decision Drivers

- **Inward dependency rule stays intact** — Application and Domain must not
  learn about Anthropic message shapes.
- **Swappable-persistence parity** — like `ITicketSimilaritySearch`, any new
  capability the edge consumes needs an Application port plus a Postgres and an
  InMemory implementation, or `Tests.EndToEnd` can no longer run both providers.
- **Honest degradation** — memory needs embeddings; without a Voyage key it must
  report unavailable rather than fake a stored or recalled fact (mirrors
  `find_similar_tickets`, ADR 0024).
- **Auth-ready without over-building** — there is no authentication yet
  (ADR 0022), but ownership must be modelled so real auth later plugs in without
  a schema or contract change.

## Decision Outcome

### Short-term: conversation state moves server-side

- The transcript contract changes: `AssistantChatRequest` now carries a
  `ConversationId?` plus the single new user message. The server returns the id
  on the first turn as a new `conversation` SSE event; the client echoes it on
  each subsequent turn.
- `IConversationStore` (Application port, EF + InMemory implementations) loads
  and appends messages, scoped by owner. A conversation is just its ordered
  messages plus an owner — there is no aggregate with invariants, so no domain
  type and no parent table.
- A message's content is stored as an **opaque, edge-owned string**. The edge
  serializes/deserializes it; the store never interprets it, so Anthropic types
  stay at the HTTP edge. Only user/assistant **text** turns are persisted — this
  matches the fidelity the client-resend already had (it never resent
  `tool_use`/`tool_result` blocks); the in-loop tool blocks remain request-local.
- A turn is persisted only on successful completion, so a failed or aborted turn
  leaves no dangling user message and can be retried on the same conversation.

### Long-term: embedded memory behind ports

- `IMemoryStore` (write) and `IMemorySearch` (recall) are Application ports.
  `PgVectorMemoryStore` implements both: `AddAsync` embeds the content
  synchronously (Voyage *Document*) so a fact is recallable in the same session;
  `SearchAsync` embeds the query (Voyage *Query*) and ranks the owner's memories
  by cosine distance. No background worker — writes are synchronous and low-volume.
- Without a Voyage key (or on InMemory) the store reports `IsAvailable = false`;
  the tools relay that to the model instead of inventing results.
- Two tools follow the standard `<Name>Tool.cs` + `<Name>Tool.prompt.cs` pattern:
  `remember` (store a durable fact) and `recall_memory` (semantic lookup),
  registered in `AssistantComposition`.

### Ownership seam

`ICurrentUser` resolves the `OwnerId` on whose behalf the assistant acts. Today
`DemoCurrentUser` returns a constant owner (no auth). Real auth replaces only
that class with one reading the security principal — conversations and memories
are already owner-scoped in storage, so nothing downstream changes.

## Key mechanics

- **New SSE event `conversation`** is emitted first on every stream, carrying the
  id so the client can continue the conversation.
- **Owner scoping is enforced in the store**, not the caller: an unknown id or a
  mismatched owner yields an empty transcript — never another owner's data
  (covered by an `EndToEnd` test on both providers).
- **One migration** (`AddAgentMemory`) adds `ConversationMessages` and `Memories`
  (the latter with a `vector(1024)` column, same width as ticket embeddings).

## What is intentionally missing

- **Conversation resume UI** — the transcript is persisted, but the web client
  still shows only the in-session history; it does not reload a prior
  conversation. The `conversationId` is held in page state, not storage.
- **Full block-level transcript fidelity** — persisting `tool_use`/`tool_result`
  blocks across turns was not needed to match prior behaviour and would pull
  Anthropic types into the store.
- **Memory editing / expiry / dedup** — memories are write-once; curation is out
  of scope.
- **Per-user isolation in practice** — with one demo owner, all state shares a
  namespace until real auth lands.

## Consequences

### Positive

- Request size no longer grows with conversation length.
- Memory and conversation state ride the same swappable-persistence tests as the
  rest of the system; both providers stay in parity.
- Auth becomes an isolated follow-up: implement `ICurrentUser`, nothing else.

### Negative

- The API is no longer stateless; a conversation now has server-side lifetime
  (unbounded — no retention policy yet).
- Opaque `Content` storage means the persisted transcript is only meaningful to
  the edge that wrote it.

## Re-evaluation Triggers

Revisit when:

1. **Auth arrives** — swap `DemoCurrentUser`; revisit whether conversations need
   a retention/cleanup policy.
2. **Conversations must be resumable in the UI** — the web client needs a load
   path and the transcript may need richer fidelity.
3. **Memory volume grows** — add a vector index and possibly curation/expiry.

## Related

- ADR 0023 – AI Assistant as Edge Adapter (this reverses its stateless stance)
- ADR 0024 – Semantic Ticket Search (same Voyage + pgvector + honest-degrade pattern)
- ADR 0022 – Security/Hardening Minimal (no real auth; the `ICurrentUser` seam)
- ADR 0007 – Swappable Persistence (why the stores need both providers)
- `src/ServiceDeskLite.Application/Abstractions/Assistant/`
- `src/ServiceDeskLite.Api/Assistant/RememberTool.cs`, `RecallMemoryTool.cs`
- `docs/operations/runbook.md` – setup and demo flow
