## AI Assistant (`ServiceDeskLite.Api`)

How the assistant is built, part by part: the loop, the tools, retrieval, the guards and what an operator sees.
Each part names the ADR that records the decision behind it.
The [README](https://github.com/goldbarth/ServiceDeskLite#the-assistant-experiment) has the short version, and says how this part of the repository came about.

`POST /api/v1/assistant/chat` drives an agentic loop against the Anthropic Messages API (official .NET SDK) and re-streams the result as Server-Sent Events.
The design goal: the model decides *what* to do, but *can only act* through the existing application layer.

```text
Browser ─POST msg + convId─▶ API adapter ──stream──▶ Anthropic Messages API
   ▲                             │
   │  SSE: conversation / text / │  stop_reason: tool_use?
   │  tool_call / tool_result /  ▼
   │  citation /  ┌─ find_similar_tickets ─▶ hybrid: pgvector + keyword (RRF)
   │  done        ├─ search_knowledge_base ▶ KB corpus chunks (Voyage + pgvector)
   │              ├─ recall_memory / remember ▶ long-term memory (pgvector)
   │              ├─ search_tickets ───────▶ SearchTicketsHandler (filter/sort/page)
   │              ├─ change_ticket_status ─▶ ChangeTicketStatusHandler (state machine)
   │              ├─ assign_ticket ────────▶ AssignTicketHandler (agent roster, FK)
   │              ├─ route_ticket ─────────▶ RouteTicketHandler (auto-triage via handlers)
   └──────────────┴─ CreateTicketHandler / UpdateTicketHandler
                     (validation, audit, outbox - unchanged)
   conversation state persisted server-side (IConversationStore); client resends only convId
```

**Non-blocking token streaming.** Text deltas are forwarded to the browser the moment they arrive - the stream is never buffered until completion.
Tool-use blocks arrive interleaved in the same stream as partial JSON fragments (`input_json_delta`); the adapter accumulates them per content block and parses the input only when the block closes.
Streaming text and assembling tool calls happen concurrently on one pass over the stream, so the user watches the model "think aloud" while its tool arguments are still being assembled.

**The loop.** When a turn ends with `stop_reason: tool_use`, the adapter executes each requested tool, appends the assistant turn plus all tool results to the message history, and calls the model again - up to a configurable iteration cap.
Twelve tools are exposed:

- `add_comment` - write on a ticket without changing it: a follow-up question, a proposed solution, or the reasoning behind an action left for a human to approve.
- `find_similar_tickets` - hybrid duplicate check before creating a ticket, semantic and keyword fused, with optional status and priority filters.
- `search_knowledge_base` - semantic retrieval over the knowledge-base corpus (articles, FAQ, internal docs), to ground how-to answers in cited sources.
- `check_grounding` - verify a drafted answer against the retrieved sources before sending it.
- `search_tickets` - find existing tickets by structured filter (status, priority, assignee) plus free text, returning a compact list to act on by id.
- `create_ticket` - file a ticket from the user's description.
- `update_ticket` - partial update of any existing ticket by id, resolved from an earlier `create_ticket` result or via `search_tickets`, e.g. "set the login ticket to high priority"; only provided fields change.
- `change_ticket_status` - move a ticket through the workflow, e.g. "close the printer ticket"; the domain state machine rejects invalid transitions and the reason is relayed to the model.
- `assign_ticket` - assign, reassign or unassign by resolving an agent name against the seeded roster; an unknown or inactive agent comes back with the list of valid agents.
- `route_ticket` - auto-triage a ticket from its content into a category, priority, assignee and status with a deterministic rule-based router; a confident decision is applied through the update, assign and change-status handlers and audited, a weak one is returned as a suggestion the model confirms rather than committing ([ADR-0032](../adr/0032-ai-auto-routing.md)).
- `remember` and `recall_memory` - store and semantically recall durable user facts across conversations.

**Memory across turns and sessions.** Short-term: conversation state is persisted server-side ([`IConversationStore`](https://github.com/goldbarth/ServiceDeskLite/blob/main/src/ServiceDeskLite.Application/Abstractions/Assistant/IConversationStore.cs), Postgres + InMemory), so the client sends only the new message plus a `conversationId` (returned on the first turn via a `conversation` SSE event) instead of the whole transcript.
Long-term: `remember` embeds a durable fact (Voyage) and `recall_memory` retrieves it by cosine similarity from pgvector, scoped to an owner resolved through the `ICurrentUser` seam (a constant demo owner today; real auth swaps only that).
Without a Voyage key - or on InMemory - memory reports itself unavailable rather than faking a stored or recalled fact.
Design and scope: [ADR-0026](../adr/0026-agent-memory.md).

**RAG as an agent tool.** Before creating a ticket, the model is instructed to check for duplicates: the query is embedded (Voyage AI, `voyage-3.5` - Anthropic has no embeddings endpoint) and ranked by cosine distance against ticket embeddings stored in pgvector, inside the existing PostgreSQL. Retrieval is cross-lingual - a German problem description matches English tickets.
Indexing is asynchronous: a poll-based background worker embeds new, edited (content-hash staleness check), and backfilled tickets in batches, so the ticket write path gains no network dependency.
Without a Voyage key - or on the InMemory provider - the tool honestly reports search as unavailable instead of faking empty results.
Design and deliberate scope cuts (no chunking, no re-ranking, no separate vector DB): [ADR-0024](../adr/0024-semantic-ticket-search-rag.md).

**Knowledge-base RAG with cited answers.** For how-to and policy questions, the model consults a `/KnowledgeBase` corpus (markdown articles, FAQ, internal docs) via `search_knowledge_base`.
A poll-based worker splits each article into section chunks (on `##` headings), embeds them (Voyage) and indexes them in pgvector - pruning chunks when a file is edited or removed - mirroring the ticket-embedding pipeline.
Retrieved passages ground the answer and are streamed to the client as a distinct `citation` SSE event (title, source, heading, snippet).
The web client anchors each cited passage as an inline `[n]` badge after the quoted title in the rendered Markdown - with a hover/focus tooltip - and lists the same numbered passages as a "Sources" card below the reply; the badge is injected into the sanitized Markdown through a private-use sentinel the model's own text cannot forge, so the `MarkupString` XSS hardening holds.
Honest by construction: without a Voyage key - or on InMemory - search reports unavailable and the tool emits *no* citations rather than fabricating a source.
Design and deliberate scope cuts: [ADR-0029](../adr/0029-knowledge-base-rag.md).

**Grounding check against hallucination.** A cited answer can still assert what its source never said.
Before the model sends an answer built on knowledge-base passages, it calls `check_grounding` with its *draft*: each substantive sentence is scored against the passages actually retrieved this turn (held in a per-request `IRagRetrievalContext`), and the unsupported ones come back.
Scoring is semantic - each sentence is embedded and matched to its nearest passage by cosine similarity, so a correct paraphrase or a cross-lingual answer grounds where word overlap would have failed it ([ADR-0040](../adr/0040-semantic-grounding-evaluation.md)); without a Voyage key it falls back to the lexical evaluator.
On a weak score the model re-retrieves, drops the claim, or hedges - self-correcting in the loop rather than asserting.
The call is not left to the prompt: once passages have been retrieved and no check has run, the loop forces it via `tool_choice`, so the verdict is computed before the first token streams ([ADR-0039](../adr/0039-grounding-check-enforcement.md)).
Checking the draft (not the streamed answer) keeps token streaming intact; the score rides the `tool_result` event and renders as a grounding badge.
Design and thresholds: [ADR-0031](../adr/0031-rag-grounding-evaluation.md), [ADR-0040](../adr/0040-semantic-grounding-evaluation.md).

**Self-correction instead of silent failure.** Tool inputs are parsed and guarded before touching the domain (schema shape, priority enum, due dates in the past).
A rejected input - or a handler `Result` failure - is returned to the model as a `tool_result` with `is_error: true`, including the reason; the model then retries with corrected arguments within the same loop.
LLM output is treated as untrusted input, never piped raw into business logic.

**Autonomous chains with transient retry.** A single request can trigger a whole sequence in one turn - check for duplicates, create if new, then assign - the model plans the steps and uses each result to decide the next (prompt-driven; the loop is the planner).
Deterministic failures surface as `is_error` for the model to fix; *transient* failures (a Voyage `429`, an upstream `5xx`, a timeout) are retried at the edge with bounded exponential backoff before surfacing, so a rate-limit blip mid-chain recovers instead of aborting the step.
Retry re-invokes the same command handlers - nothing bypasses domain validation.
Design and scope: [ADR-0027](../adr/0027-agent-orchestration-and-retry.md).

**Self-critique, confidence, and fallback.** Retrieval tools attach a confidence signal (top-match relevance) to their result - logged and emitted on the `tool_result` SSE event for observability - and the prompt tells the model to distrust weak, empty, or contradictory results and re-plan in the same turn rather than asserting a false duplicate.
When the semantic signal cannot run (no Voyage key / InMemory), hybrid retrieval degrades to labelled keyword-only results instead of dead-ending - graceful, clearly marked as weaker evidence, and still routed through the application layer.
Design and scope: [ADR-0028](../adr/0028-self-critique-confidence-fallback.md).

**Hybrid ticket retrieval.** `find_similar_tickets` blends two signals - semantic (pgvector cosine) and keyword (substring) - with Reciprocal Rank Fusion (RRF), rather than using either alone: semantic catches paraphrases, keyword catches exact tokens (error codes, hostnames).
Optional status/priority metadata filters constrain both signals (the ticket has no "type" field, so priority is that dimension), priority applies a light ranking nudge, and each result carries a fused relevance score plus the signals that matched it.
Fusion lives in the Application layer, composed from the existing ports, so it is provider-agnostic and unit-tested with a ranking function tested in isolation.
Design and deliberate scope cuts (no learned ranking, no FTS index, no re-ranking): [ADR-0030](../adr/0030-hybrid-ticket-retrieval.md).

**Streaming ticket summaries.** `GET /api/v1/tickets/{id}/summary` streams a structured triage summary of one ticket - summary, next steps, risks, and missing information - rendered live in the ticket's `AI Summary` tab.
Structure and token-by-token streaming pull against each other: a JSON schema would guarantee the shape but reach the client as partial JSON that cannot be rendered progressively.
Instead the model writes marker-delimited prose, and the API recovers the section boundaries from the token stream, re-emitting each fragment as an SSE `delta` tagged with its section - so all four panels fill as the text arrives.
Markers do not arrive whole (`<<NEXT_` then `STEPS>>`), so the parser holds back any viable marker prefix and never leaks a fragment into the UI. This is a read, not an action: it is a separate endpoint with no tools, deliberately outside the assistant's agentic loop.
Whatever the ticket does not say lands in *missing information* rather than being invented in the summary.
Design and scope cuts: [ADR-0033](../adr/0033-streaming-ticket-summaries.md).

**Agent sandbox.** Every tool call passes a guard pipeline before it reaches a command handler: unknown tool names are refused, argument size is capped before anything parses it, writes are budgeted per chat turn, and tool calls and model turns are rate limited per owner with in-process token buckets.
The pipeline sits at the single point where the model's intent becomes execution, so a tool cannot be added past it - unlike a base class a new tool may simply not inherit from.
Guards separate `Check` from `Commit` and nothing is spent until every guard has admitted the call, so a rate-limit token is never burned on a write the write budget then rejects.
A refusal is not a failure: it returns to the model as an ordinary `is_error` tool result with the reason, and the model explains to the user what it did not do.
Structured error propagation was already in place - `ToolRetryPolicy` turns any non-transient tool exception into an `is_error` result, so the stream never breaks.
Design and the reversal of the earlier "no rate limiting" stance: [ADR-0035](../adr/0035-agent-sandbox.md).

**AI operations dashboard.** `GET /api/v1/dashboard/ai` reports what the assistant actually did over the last seven days - automation rate, duplicate-check hit rate, retrieval confidence, per-tool call statistics, and token usage - rendered on the `AI Insights` page.
The automation rate needs no new plumbing: the assistant reaches the domain only through the same command handlers as everyone else, so its work is audited like everyone else's and the actor is the entire difference.
Tool calls and token usage are captured as they happen, on a sink that writes on its own DbContext (a telemetry `SaveChanges` must never commit a failed command's staged entities) and swallows its own failures (a dropped metric costs a dashboard row; a thrown one costs the user's chat turn).
Every rate is nullable to the wire and renders as `n/a`: a system nobody has used has not achieved 0 % automation, and where semantic retrieval is unconfigured, confidence is not low but unmeasurable.
Design and scope cuts: [ADR-0034](../adr/0034-ai-operations-metrics.md).

**An autonomous worker, fenced in.** A background loop reviews open tickets on a schedule: it asks for missing information, parks the ticket while it waits, proposes solutions grounded in the knowledge base, and refers every high-impact decision to a person ([ADR-0037](../adr/0037-autonomous-ticket-worker.md)).
It is the same agent loop, the same tools and the same command handlers as the chat assistant - extracting that loop rather than writing a second one is the point: every bound the sandbox places on the agent would otherwise have to be remembered twice, and nothing would fail when it was not.
Two things differ, and both come from the scope the worker opens per ticket.
It audits as `ai-worker`, because reading the trail, "the assistant did this while I was talking to it" and "a background process decided this without me" are not the same event.
And a review guard constrains it: reads are always allowed, `add_comment` is allowed because it changes nothing and is how the worker reaches a person, triaging and parking are allowed because they sort a ticket without finishing it - everything else comes back as a refusal that names the way out.
The model reads it, posts its proposal as a comment, and a human decides.
That refusal is the whole guardrail, and it reuses the mechanism ADR-0035 already established: a refused call is not a failure, it is an instruction the model follows.
What it may do unattended is validated configuration, not a sentence in a prompt, and the worker is off unless switched on.

**Observability for operators.** The same signals the dashboard aggregates are also exposed as OpenTelemetry metrics on a `GET /metrics` Prometheus endpoint (outside the API-key guard - a scraper is infrastructure, and the endpoint carries only aggregate counters, no ticket content) and as distributed traces.
A decorator over the metrics sink is the single recording point, so the dashboard and Prometheus read the same measurement and cannot drift; the instruments live on a BCL `Meter` and `ActivitySource`, so the recording code names no backend and the composition root alone decides who scrapes or receives them.
Tool calls carry an `error` label rather than a pre-computed error rate - a ratio recorded at record time cannot be re-aggregated across windows, so the rate is a query-time division.
Each conversation is one `assistant.chat` span with a child per model turn and per tool call, so a write is traceable to the ticket it changed and a guard refusal to the rule that refused it; trace export stays off until an OTLP endpoint is configured.
Design and the confidence-histogram cut that keeps a routing score out of the retrieval buckets: [ADR-0036](../adr/0036-observability.md).

**Conversation state and time.** Conversation state is persisted server-side ([`IConversationStore`](https://github.com/goldbarth/ServiceDeskLite/blob/main/src/ServiceDeskLite.Application/Abstractions/Assistant/IConversationStore.cs)): the client sends only the new message plus a `conversationId`, and the stored transcript is what lets the model reference the id of a ticket it created earlier ([ADR-0026](../adr/0026-agent-memory.md)).
Because the model has no calendar, the current date (with weekday) and the configured user timezone are injected into the system prompt per request, so relative deadlines ("by Friday") resolve correctly and due times render in the user's local time.
Vague times of day ("morning") trigger a clarifying question rather than a guess.
