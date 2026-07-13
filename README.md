<div align="center">

# ServiceDeskLite

**A .NET 10 Clean Architecture reference with an AI agent that works on its own - and cannot reach past the domain.**

Twelve tools, hybrid RAG with cited answers, a guard pipeline no tool can be added past,
and an autonomous worker that refers every high-impact decision to a person.

[![Release](https://img.shields.io/github/v/release/goldbarth/ServiceDeskLite?style=flat-square&label=release&color=2f6f4e)](https://github.com/goldbarth/ServiceDeskLite/releases/latest)
[![CI](https://img.shields.io/github/actions/workflow/status/goldbarth/ServiceDeskLite/ci.yml?branch=main&style=flat-square&label=CI)](https://github.com/goldbarth/ServiceDeskLite/actions/workflows/ci.yml)
[![Docs](https://img.shields.io/github/actions/workflow/status/goldbarth/ServiceDeskLite/docs.yml?branch=main&style=flat-square&label=docs)](https://goldbarth.github.io/ServiceDeskLite/)
[![ADRs](https://img.shields.io/badge/ADRs-37-2f6f4e?style=flat-square)](https://goldbarth.github.io/ServiceDeskLite/adr/index.html)
[![License](https://img.shields.io/github/license/goldbarth/ServiceDeskLite?style=flat-square)](LICENSE)

![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?style=flat-square&logo=dotnet&logoColor=white)
![PostgreSQL + pgvector](https://img.shields.io/badge/PostgreSQL-pgvector-4169E1?style=flat-square&logo=postgresql&logoColor=white)
![Claude](https://img.shields.io/badge/Claude-tool%20calling-D97757?style=flat-square&logo=claude&logoColor=white)
![Voyage AI](https://img.shields.io/badge/Voyage%20AI-embeddings-1a1a1a?style=flat-square)
![OpenTelemetry](https://img.shields.io/badge/OpenTelemetry-traces-425CC7?style=flat-square&logo=opentelemetry&logoColor=white)

<!--
  Demo GIF goes here after the UI/UX redesign.
  Drop it at docs/assets/demo.gif and uncomment:

  <img src="docs/assets/demo.gif" alt="The assistant files a ticket from a free-text report, checks for duplicates first" width="880" />

  Keep it under ~5 MB so GitHub does not lazy-load it out of the fold.
-->

</div>

A .NET 10 service-desk reference application built on Clean Architecture: explicit domain rules, result-based error handling, swappable persistence, and an AI agent that turns free-text problem reports into tickets via LLM tool calling - checking for duplicates first through semantic search (RAG), streamed live to a Blazor frontend, and working open tickets on its own behind a human-review guard. The surface has grown across releases; what has not changed is the structure underneath: explicit boundaries, reviewable design decisions, and every feature reaching the domain through the same handlers.

> **See it running:** the [runbook](docs/operations/runbook.md) gets you from clone to the web UI (including the AI assistant) in under five minutes.
> **Full documentation** - architecture, all ADRs, API reference, testing strategy - lives on the [documentation site](https://goldbarth.github.io/ServiceDeskLite/).

## Core capabilities

- **Ticket workflow** - create, search (filter/sort/paging), partial update, workflow-validated status transitions, assignment, and comments, enforced by an explicit domain state machine
- **AI agent** - users describe issues in free text; Claude decides via tool calling what to do, across twelve tools (create, search, update, status, assign, comment, auto-route, ticket retrieval, knowledge-base retrieval, grounding check, remember/recall), and every tool call executes through the same application-layer command handlers as the REST API
- **Autonomous ticket worker** - a background loop reviews open tickets on a schedule, asks for missing information, parks the ticket while it waits, and proposes solutions grounded in the knowledge base; it runs the same loop, tools and guards as the chat assistant, and a review guard refers every high-impact write to a person ([ADR-0037](docs/adr/0037-autonomous-ticket-worker.md))
- **Agent sandbox** - every tool call passes a guard pipeline at the single admission point where model intent becomes execution: unknown tool names refused, argument size capped, writes budgeted per turn, tool calls and model round trips rate limited per owner; a refusal returns to the model as an ordinary error result, not a broken stream ([ADR-0035](docs/adr/0035-agent-sandbox.md))
- **Hybrid ticket retrieval (RAG)** - before creating a ticket, the agent checks for duplicates by meaning and by exact token: semantic (Voyage embeddings in PostgreSQL/pgvector, cosine) and keyword signals are fused with Reciprocal Rank Fusion, cross-lingual, so a German query finds English tickets ([ADR-0030](docs/adr/0030-hybrid-ticket-retrieval.md))
- **Knowledge-base RAG with cited answers** - a `/KnowledgeBase` corpus is chunked, embedded and indexed by a background worker; how-to answers stream the passages they cited, and `check_grounding` scores the draft against those passages so the agent hedges or re-retrieves instead of asserting an unsupported claim ([ADR-0029](docs/adr/0029-knowledge-base-rag.md), [ADR-0031](docs/adr/0031-rag-grounding-evaluation.md))
- **Agent memory** - conversation state is persisted server-side, and durable user facts are embedded and semantically recalled across conversations ([ADR-0026](docs/adr/0026-agent-memory.md))
- **AI auto-routing and summaries** - incoming tickets are triaged into category, priority, assignee and status, with an uncertain decision returned as a suggestion rather than a silent write; a ticket's history streams into a structured summary of next steps, risks and missing information ([ADR-0032](docs/adr/0032-ai-auto-routing.md), [ADR-0033](docs/adr/0033-streaming-ticket-summaries.md))
- **Observability** - a `GET /metrics` Prometheus endpoint and OpenTelemetry traces expose tool latency, error rates, token usage and retrieval confidence; an AI Insights page reports what the agent actually did, and an unmeasurable rate renders as `n/a` rather than as zero ([ADR-0034](docs/adr/0034-ai-operations-metrics.md), [ADR-0036](docs/adr/0036-observability.md))
- **Streaming end to end** - model output reaches the browser token by token over Server-Sent Events; tool activity is surfaced as typed events while the stream stays open
- **Audit trail** - every state change raises a domain event that is persisted as an audit record, including changes made by the assistant (actor `ai-assistant`) and by the autonomous worker (actor `ai-worker`)
- **Transactional outbox (stub)** - domain events are staged as outbox messages in the same transaction as the state change; deliberately without a dispatcher ([ADR-0021](docs/adr/0021-outbox-stub.md))
- **Result-based application flow** - no exceptions cross application boundaries; failures map to RFC 9457 ProblemDetails with machine-readable error codes and field-level validation
- **Swappable persistence** - PostgreSQL (EF Core) and InMemory behind the same application ports; end-to-end tests run against both
- **Agent evaluation suite** - the real assistant endpoint, loop, guards, tools and handlers run against a scripted model; only the HTTP transport under the Anthropic client is replaced, so tool calling, RAG grounding and streaming are pinned in CI without a network call

## System architecture

```text
┌─────────────────────────────────────┐
│              Web (Blazor)           │  MudBlazor UI, SSE consumer (chat)
├─────────────────────────────────────┤
│           API (Minimal API)         │  Endpoints, ProblemDetails, AI adapter
├───────────────────┬─────────────────┤
│  Infrastructure   │  Infra.InMemory │  EF Core/PostgreSQL │ in-process store
├───────────────────┴─────────────────┤
│           Application               │  Use cases, validation, ports, UoW
├─────────────────────────────────────┤
│              Domain                 │  Entities, workflow rules, events
└─────────────────────────────────────┘
```

Dependency direction is strictly inward: Domain knows nothing about Application, Application nothing about API or Infrastructure. The LLM integration lives entirely at the edge (API layer) - a deep dive is in the [architecture overview](https://goldbarth.github.io/ServiceDeskLite/architecture/overview.html).

## AI assistant - tool calling, streaming & RAG

`POST /api/v1/assistant/chat` drives an agentic loop against the Anthropic Messages API (official .NET SDK) and re-streams the result as Server-Sent Events. The design goal: the model decides *what* to do, but *can only act* through the existing application layer.

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

**Non-blocking token streaming.** Text deltas are forwarded to the browser the moment they arrive - the stream is never buffered until completion. Tool-use blocks arrive interleaved in the same stream as partial JSON fragments (`input_json_delta`); the adapter accumulates them per content block and parses the input only when the block closes. Streaming text and assembling tool calls happen concurrently on one pass over the stream, so the user watches the model "think aloud" while its tool arguments are still being assembled.

**The loop.** When a turn ends with `stop_reason: tool_use`, the adapter executes each requested tool, appends the assistant turn plus all tool results to the message history, and calls the model again - up to a configurable iteration cap. Twelve tools are exposed: `add_comment` (write on a ticket without changing it - a follow-up question, a proposed solution, or the reasoning behind an action left for a human to approve), `find_similar_tickets` (hybrid duplicate check before creating a ticket - semantic + keyword fused, with optional status/priority filters), `search_knowledge_base` (semantic retrieval over the knowledge-base corpus - articles, FAQ, internal docs - to ground how-to answers in cited sources), `check_grounding` (verify a drafted answer against the retrieved sources before sending it), `search_tickets` (find existing tickets by structured filter - status, priority, assignee - plus free text, returning a compact list to act on by id), `create_ticket` (file a ticket from the user's description), `update_ticket` (partial update of any existing ticket by id - resolved from an earlier `create_ticket` result or via `search_tickets`, e.g. "set the login ticket to high priority"; only provided fields change), `change_ticket_status` (move a ticket through the workflow, e.g. "close the printer ticket"; the domain state machine rejects invalid transitions and the reason is relayed to the model), `assign_ticket` (assign/reassign/unassign by resolving an agent name against the seeded roster; an unknown or inactive agent comes back with the list of valid agents), `route_ticket` (auto-triage a ticket from its content into a category, priority, assignee and status - a deterministic rule-based router; a confident decision is applied through the update/assign/change-status handlers and audited, a weak one is returned as a suggestion the model confirms rather than committing, see [ADR-0032](docs/adr/0032-ai-auto-routing.md)), and the long-term memory pair `remember` / `recall_memory` (store and semantically recall durable user facts across conversations).

**Memory across turns and sessions.** Short-term: conversation state is persisted server-side ([`IConversationStore`](src/ServiceDeskLite.Application/Abstractions/Assistant/IConversationStore.cs), Postgres + InMemory), so the client sends only the new message plus a `conversationId` (returned on the first turn via a `conversation` SSE event) instead of the whole transcript. Long-term: `remember` embeds a durable fact (Voyage) and `recall_memory` retrieves it by cosine similarity from pgvector, scoped to an owner resolved through the `ICurrentUser` seam (a constant demo owner today; real auth swaps only that). Without a Voyage key - or on InMemory - memory reports itself unavailable rather than faking a stored or recalled fact. Design and scope: [ADR-0026](docs/adr/0026-agent-memory.md).

**RAG as an agent tool.** Before creating a ticket, the model is instructed to check for duplicates: the query is embedded (Voyage AI, `voyage-3.5` - Anthropic has no embeddings endpoint) and ranked by cosine distance against ticket embeddings stored in pgvector, inside the existing PostgreSQL. Retrieval is cross-lingual - a German problem description matches English tickets. Indexing is asynchronous: a poll-based background worker embeds new, edited (content-hash staleness check), and backfilled tickets in batches, so the ticket write path gains no network dependency. Without a Voyage key - or on the InMemory provider - the tool honestly reports search as unavailable instead of faking empty results. Design and deliberate scope cuts (no chunking, no re-ranking, no separate vector DB): [ADR-0024](docs/adr/0024-semantic-ticket-search-rag.md).

**Knowledge-base RAG with cited answers.** For how-to and policy questions, the model consults a `/KnowledgeBase` corpus (markdown articles, FAQ, internal docs) via `search_knowledge_base`. A poll-based worker splits each article into section chunks (on `##` headings), embeds them (Voyage) and indexes them in pgvector - pruning chunks when a file is edited or removed - mirroring the ticket-embedding pipeline. Retrieved passages ground the answer and are streamed to the client as a distinct `citation` SSE event (title, source, heading, snippet), rendered as a "Sources" card. Honest by construction: without a Voyage key - or on InMemory - search reports unavailable and the tool emits *no* citations rather than fabricating a source. Design and deliberate scope cuts: [ADR-0029](docs/adr/0029-knowledge-base-rag.md).

**Grounding check against hallucination.** A cited answer can still assert what its source never said. Before the model sends an answer built on knowledge-base passages, it calls `check_grounding` with its *draft*: a deterministic evaluator (`GroundingEvaluator`) scores how much of the draft is lexically supported by the passages actually retrieved this turn (held in a per-request `IRagRetrievalContext`), and returns the unsupported sentences. On a weak score the model re-retrieves, drops the claim, or hedges - self-correcting in the loop rather than asserting. The call is not left to the prompt: once passages have been retrieved and no check has run, the loop forces it via `tool_choice`, so the verdict is computed before the first token streams ([ADR-0039](docs/adr/0039-grounding-check-enforcement.md)). Checking the draft (not the streamed answer) keeps token streaming intact; the score rides the `tool_result` event and renders as a grounding badge. The evaluator is fixture-tested with no model call. Design, thresholds, and the lexical/same-language limitation: [ADR-0031](docs/adr/0031-rag-grounding-evaluation.md).

**Self-correction instead of silent failure.** Tool inputs are parsed and guarded before touching the domain (schema shape, priority enum, due dates in the past). A rejected input - or a handler `Result` failure - is returned to the model as a `tool_result` with `is_error: true`, including the reason; the model then retries with corrected arguments within the same loop. LLM output is treated as untrusted input, never piped raw into business logic.

**Autonomous chains with transient retry.** A single request can trigger a whole sequence in one turn - check for duplicates, create if new, then assign - the model plans the steps and uses each result to decide the next (prompt-driven; the loop is the planner). Deterministic failures surface as `is_error` for the model to fix; *transient* failures (a Voyage `429`, an upstream `5xx`, a timeout) are retried at the edge with bounded exponential backoff before surfacing, so a rate-limit blip mid-chain recovers instead of aborting the step. Retry re-invokes the same command handlers - nothing bypasses domain validation. Design and scope: [ADR-0027](docs/adr/0027-agent-orchestration-and-retry.md).

**Self-critique, confidence, and fallback.** Retrieval tools attach a confidence signal (top-match relevance) to their result - logged and emitted on the `tool_result` SSE event for observability - and the prompt tells the model to distrust weak, empty, or contradictory results and re-plan in the same turn rather than asserting a false duplicate. When the semantic signal cannot run (no Voyage key / InMemory), hybrid retrieval degrades to labelled keyword-only results instead of dead-ending - graceful, clearly marked as weaker evidence, and still routed through the application layer. Design and scope: [ADR-0028](docs/adr/0028-self-critique-confidence-fallback.md).

**Hybrid ticket retrieval.** `find_similar_tickets` blends two signals - semantic (pgvector cosine) and keyword (substring) - with Reciprocal Rank Fusion (RRF), rather than using either alone: semantic catches paraphrases, keyword catches exact tokens (error codes, hostnames). Optional status/priority metadata filters constrain both signals (the ticket has no "type" field, so priority is that dimension), priority applies a light ranking nudge, and each result carries a fused relevance score plus the signals that matched it. Fusion lives in the Application layer, composed from the existing ports, so it is provider-agnostic and unit-tested with a ranking function tested in isolation. Design and deliberate scope cuts (no learned ranking, no FTS index, no re-ranking): [ADR-0030](docs/adr/0030-hybrid-ticket-retrieval.md).

**Streaming ticket summaries.** `GET /api/v1/tickets/{id}/summary` streams a structured triage summary of one ticket - summary, next steps, risks, and missing information - rendered live in the ticket's `AI Summary` tab. Structure and token-by-token streaming pull against each other: a JSON schema would guarantee the shape but reach the client as partial JSON that cannot be rendered progressively. Instead the model writes marker-delimited prose, and the API recovers the section boundaries from the token stream, re-emitting each fragment as an SSE `delta` tagged with its section - so all four panels fill as the text arrives. Markers do not arrive whole (`<<NEXT_` then `STEPS>>`), so the parser holds back any viable marker prefix and never leaks a fragment into the UI. This is a read, not an action: it is a separate endpoint with no tools, deliberately outside the assistant's agentic loop. Whatever the ticket does not say lands in *missing information* rather than being invented in the summary. Design and scope cuts: [ADR-0033](docs/adr/0033-streaming-ticket-summaries.md).

**Agent sandbox.** Every tool call passes a guard pipeline before it reaches a command handler: unknown tool names are refused, argument size is capped before anything parses it, writes are budgeted per chat turn, and tool calls and model turns are rate limited per owner with in-process token buckets. The pipeline sits at the single point where the model's intent becomes execution, so a tool cannot be added past it - unlike a base class a new tool may simply not inherit from. Guards separate `Check` from `Commit` and nothing is spent until every guard has admitted the call, so a rate-limit token is never burned on a write the write budget then rejects. A refusal is not a failure: it returns to the model as an ordinary `is_error` tool result with the reason, and the model explains to the user what it did not do. Structured error propagation was already in place - `ToolRetryPolicy` turns any non-transient tool exception into an `is_error` result, so the stream never breaks. Design and the reversal of the earlier "no rate limiting" stance: [ADR-0035](docs/adr/0035-agent-sandbox.md).

**AI operations dashboard.** `GET /api/v1/dashboard/ai` reports what the assistant actually did over the last seven days - automation rate, duplicate-check hit rate, retrieval confidence, per-tool call statistics, and token usage - rendered on the `AI Insights` page. The automation rate needs no new plumbing: the assistant reaches the domain only through the same command handlers as everyone else, so its work is audited like everyone else's and the actor is the entire difference. Tool calls and token usage are captured as they happen, on a sink that writes on its own DbContext (a telemetry `SaveChanges` must never commit a failed command's staged entities) and swallows its own failures (a dropped metric costs a dashboard row; a thrown one costs the user's chat turn). Every rate is nullable to the wire and renders as `n/a`: a system nobody has used has not achieved 0 % automation, and where semantic retrieval is unconfigured, confidence is not low but unmeasurable. Design and scope cuts: [ADR-0034](docs/adr/0034-ai-operations-metrics.md).

**An autonomous worker, fenced in.** A background loop reviews open tickets on a schedule: it asks for missing information, parks the ticket while it waits, proposes solutions grounded in the knowledge base, and refers every high-impact decision to a person ([ADR-0037](docs/adr/0037-autonomous-ticket-worker.md)). It is the same agent loop, the same tools and the same command handlers as the chat assistant - extracting that loop rather than writing a second one is the point: every bound the sandbox places on the agent would otherwise have to be remembered twice, and nothing would fail when it was not. Two things differ, and both come from the scope the worker opens per ticket. It audits as `ai-worker`, because reading the trail, "the assistant did this while I was talking to it" and "a background process decided this without me" are not the same event. And a review guard constrains it: reads are always allowed, `add_comment` is allowed because it changes nothing and is how the worker reaches a person, triaging and parking are allowed because they sort a ticket without finishing it - everything else comes back as a refusal that names the way out. The model reads it, posts its proposal as a comment, and a human decides. That refusal is the whole guardrail, and it reuses the mechanism ADR-0035 already established: a refused call is not a failure, it is an instruction the model follows. What it may do unattended is validated configuration, not a sentence in a prompt, and the worker is off unless switched on.

**Observability for operators.** The same signals the dashboard aggregates are also exposed as OpenTelemetry metrics on a `GET /metrics` Prometheus endpoint (outside the API-key guard - a scraper is infrastructure, and the endpoint carries only aggregate counters, no ticket content) and as distributed traces. A decorator over the metrics sink is the single recording point, so the dashboard and Prometheus read the same measurement and cannot drift; the instruments live on a BCL `Meter` and `ActivitySource`, so the recording code names no backend and the composition root alone decides who scrapes or receives them. Tool calls carry an `error` label rather than a pre-computed error rate - a ratio recorded at record time cannot be re-aggregated across windows, so the rate is a query-time division. Each conversation is one `assistant.chat` span with a child per model turn and per tool call, so a write is traceable to the ticket it changed and a guard refusal to the rule that refused it; trace export stays off until an OTLP endpoint is configured. Design and the confidence-histogram cut that keeps a routing score out of the retrieval buckets: [ADR-0036](docs/adr/0036-observability.md).

**Conversation state and time.** Conversation state is persisted server-side ([`IConversationStore`](src/ServiceDeskLite.Application/Abstractions/Assistant/IConversationStore.cs)): the client sends only the new message plus a `conversationId`, and the stored transcript is what lets the model reference the id of a ticket it created earlier ([ADR-0026](docs/adr/0026-agent-memory.md)). Because the model has no calendar, the current date (with weekday) and the configured user timezone are injected into the system prompt per request, so relative deadlines ("by Friday") resolve correctly and due times render in the user's local time. Vague times of day ("morning") trigger a clarifying question rather than a guess.

## Architecture decisions

Every non-obvious choice is recorded as an ADR - 37 records, browsable on the [ADR index](https://goldbarth.github.io/ServiceDeskLite/adr/index.html). A selection:

| ADR | Decision | In short |
|-----|----------|----------|
| [0001](docs/adr/0001-hexagonal-layered-architecture.md) | Hexagonal layering | Ports & adapters with strict inward dependencies |
| [0002](docs/adr/0002-result-pattern.md) | Result pattern | Expected failures as values, not exceptions |
| [0003](docs/adr/0003-problem-details.md) | RFC 9457 ProblemDetails | One machine-readable HTTP error contract |
| [0004](docs/adr/0004-minimal-api-no-mediatr.md) | No MediatR | Plain handlers over pipeline indirection |
| [0005](docs/adr/0005-strongly-typed-ids.md) | Strongly-typed ids | `TicketId` instead of bare `Guid` |
| [0007](docs/adr/0007-swappable-persistence.md) | Swappable persistence | Same ports, two implementations, both tested |
| [0009](docs/adr/0009-deterministic-paging.md) | Deterministic paging | Stable sort keys, reproducible pages |
| [0019](docs/adr/0019-field-level-validation.md) | Field-level validation | Errors addressable per input field |
| [0020](docs/adr/0020-audit-event-payload-format.md) | Audit payload format | JSON payloads, schema-free event types |
| [0021](docs/adr/0021-outbox-stub.md) | Outbox stub | Transactional staging now, dispatch later |
| [0023](docs/adr/0023-ai-assistant-edge-adapter.md) | AI assistant as edge adapter | LLM orchestration at the edge; the model acts only through command handlers |
| [0024](docs/adr/0024-semantic-ticket-search-rag.md) | Semantic ticket search (RAG) | pgvector in the existing Postgres, async embedding worker, RAG as agent tool |

## Tech stack

| Concern | Technology |
|---------|------------|
| Runtime / language | .NET 10, C# |
| HTTP API | ASP.NET Core Minimal API, RFC 9457 ProblemDetails, SSE via `TypedResults.ServerSentEvents` |
| LLM integration | Anthropic Messages API (official `Anthropic` .NET SDK), streaming tool calling |
| Semantic search (RAG) | Voyage AI embeddings (`voyage-3.5`) + pgvector, cosine similarity, async indexing worker |
| Frontend | Blazor Server, MudBlazor, `System.Net.ServerSentEvents` client |
| Persistence | EF Core + PostgreSQL (`pgvector/pgvector:pg17`), swappable InMemory implementation |
| Logging | Serilog (console, enriched with trace ids) |
| API documentation | OpenAPI/Swagger, snapshot-checked in CI |
| Testing | xUnit, FluentAssertions, `WebApplicationFactory`-based integration and end-to-end suites |
| Packaging / ops | Docker Compose (API + PostgreSQL), NuGet lock files |

## Testing

| Project | Scope |
|---------|-------|
| `Tests.Domain` | Domain rules and workflow transitions, pure unit tests |
| `Tests.Application` | Command/query handlers against in-memory fakes |
| `Tests.Api` | Endpoint behavior via `WebApplicationFactory`, plus AI tool-input parsing |
| `Tests.Integration` | API against the InMemory infrastructure |
| `Tests.Infrastructure.InMemory` | InMemory persistence adapter |
| `Tests.Web` | Frontend API clients and feature state |
| `Tests.EndToEnd` | Full stack against both persistence implementations |

```bash
dotnet test   # no database or API key required - test hosts inject fakes
```

CI runs all suites on every push and additionally guards the OpenAPI contract against unintended changes via a snapshot check. Details: [testing overview](https://goldbarth.github.io/ServiceDeskLite/testing/overview.html).

## Out of scope (deliberately)

- Real authentication/authorization - the API key middleware is a demo-grade guard, not an identity system
- Outbox dispatching - messages are staged transactionally but not yet relayed to a broker (ADR-0021)
- Conversation persistence for the assistant - transcripts live in the browser session only
- LLM prompt caching, and rate limiting beyond the in-process per-owner buckets of the agent sandbox
- RAG refinements - chunking (tickets are short), hybrid FTS+vector search, re-ranking, and a vector index (HNSW/IVFFlat) are deliberately cut at this data size ([ADR-0024](docs/adr/0024-semantic-ticket-search-rag.md))
- Multi-language localization of the UI

## Documentation

The complete documentation is published on GitHub Pages (DocFX, docs-as-code):

- [Documentation hub](https://goldbarth.github.io/ServiceDeskLite/) - entry point
- [Architecture overview](https://goldbarth.github.io/ServiceDeskLite/architecture/overview.html)
- [ADR index](https://goldbarth.github.io/ServiceDeskLite/adr/index.html) - all 24 decision records
- [OpenAPI reference and Swagger UI](https://goldbarth.github.io/ServiceDeskLite/api/openapi.html)
- [Testing overview](https://goldbarth.github.io/ServiceDeskLite/testing/overview.html)
- [Runbook](docs/operations/runbook.md) - local setup for reviewers

## License

MIT. See [LICENSE](LICENSE).
