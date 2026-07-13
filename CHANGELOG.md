# Changelog

## v1.9.0 - Frontend Redesign: One Visual System, Power-User Speed

### Summary

This release is the M9 milestone: a cohesive overhaul of the web client, from the token scale it is drawn on up to the interactions an operator repeats all day.
Where v1.8.0 hardened what the assistant does, this one reworks what the desk sees and touches - a single design-token scale, dark mode, richer ticket signals, saved views, a command palette, inline editing, optimistic status moves, and recommended actions that are buttons instead of captions.
The assistant surface joins the same system: answers render as Markdown, and knowledge-base sources are listed under the reply.
The design-token foundation (#202) shipped early under the v1.8.0 tag; it is the base every other M9 change is drawn on, and this is the release where the redesign it enabled is complete.

### Highlights

- One design-token scale for spacing, radius, elevation, and semantic color, with a guard test that fails the build on a raw literal where a token applies (#202, tagged in v1.8.0)
- Dark mode with a PaletteDark, token schemes, and a top-bar toggle - both themes first-class (#203)
- Status and priority render as chips with an icon, not color alone (#204); the queue has a fixed row height and a monospaced reference (#205)
- Saved views: segmented presets over the ticket list that filter without a dialog round-trip (#206)
- Command palette on Ctrl/Cmd+K: navigates from the NavRegistry, finds tickets through the search endpoint (debounced, cancellable), fully keyboard-operable (#210)
- Inline status/assignee edit instead of modal dialogs (#208); optimistic status moves with rollback and the rejection code on the board and detail view (#209); the first suggested step is a primary button that runs it (#211, ADR 0041)
- Assistant answers render as sanitized Markdown (#196), knowledge-base sources are listed under the reply (#207), and the grounding check is kept out of the transcript (#189)
- Redesign defects fixed: category header overlapping Due (#214/#228), the composer send button outside its panel (#215), board-card titles never clamping (#216)

### Known limitations

- Inline citation badges anchored in the answer text are deferred (#243); the assistant lists sources under the Markdown reply instead
- Ticket search matches case-sensitively across the app-bar search, ticket list, and palette; fix + match-semantics decision tracked as an ADR issue (#241)
- Scoped stylesheet rules targeting MudBlazor component roots silently do nothing (Blazor CSS isolation); one case fixed (#215), an audit + guard test is tracked (#242)
- Still no real auth, no outbox dispatching, no HTTP-level rate limiting; conversation resume UI still not built

_Full notes: [docs/releases/v1.9.0.md](docs/releases/v1.9.0.md)_

## v1.8.0 - Assistant Hardening: Grounding as a Mechanism, Honest Writes

### Summary

v1.7.0 ended with a recorded manual test pass and six open defects; this release is what working through that list produced.
Five of the six are fixed - the sixth (#196, markdown rendering) is a web concern and moves with the frontend redesign.
The structural change is bigger than any single fix: the grounding check is now enforced by the loop instead of requested by the prompt, and it scores meaning instead of counting words.
One policy now governs both write-failure directions the field pass caught: never claim a change without a successful tool result, and never re-ask for a change the user already ordered.
All of it shipped as its own milestone (M10 - Assistant Hardening); the frontend redesign collects separately on the `m9-frontend-redesign` integration branch.

### Highlights

- The grounding check is a mechanism: once knowledge-base passages exist and no check has run, the loop forces `check_grounding` via `tool_choice`, so the verdict is computed before the first token streams (ADR 0039, #187)
- Grounding scores meaning, not word overlap: per-sentence embedding similarity, so a correct paraphrase or a German answer against an English knowledge base no longer fails its own verification; the lexical evaluator remains as fallback (ADR 0040, #188)
- Write honesty and confirmation policy in the system prompt: never state a change happened without a successful tool result, never re-ask for a change already ordered (#190, #195)
- Every tool description states when to call it, locked by a build-time guard test (#186)
- The agent roster is in the system prompt, from the same query `assign_ticket` validates against - no more discovery by failed call, no more invented agent names (#193)
- A failed knowledge-base search reads as an error, distinct from honest "unavailable" degradation (#191)
- Summary timestamps are rendered in the user timezone instead of UTC (#194)

### Known limitations

- Assistant markdown still renders raw in the web client (#196), and the grounding chip contradicts the prompt (#189) - both scheduled with M9
- The semantic grounding path is not deterministic; exact reproducibility belongs to the lexical fallback and the fixture tests
- The live tool-call-rate measurement #186 asked for remains unbuilt; the cheap mitigation shipped first so a future measurement runs against the improved baseline
- Still no real auth, no outbox dispatching, no HTTP-level rate limiting

_Full notes: [docs/releases/v1.8.0.md](docs/releases/v1.8.0.md)_

## v1.7.0 - Sonnet 5, Container Fixes & a Recorded Test Pass

### Summary

The first six releases each closed a roadmap milestone; this one closes none.
It is what a manual pass over the finished assistant surface produced, tagged before the frontend redesign opens a wide diff across the web layer.
The model the assistant runs on became a decision instead of the default the first request happened to be written with, and thinking was disabled explicitly rather than inherited.
Two container defects were fixed - one that made `docker compose up` fail to bind on any host already running Postgres, and one that printed a red error line above an otherwise healthy boot.
The pass itself was written down, including the six defects it found that this release does not fix.

### Highlights

- The assistant runs on `claude-sonnet-5`: near-Opus quality on tool-calling work, the same 1M context window, roughly 40% of the bill, and a visibly faster first SSE delta (ADR 0038)
- Thinking is set explicitly at both call sites. Sonnet 5 enables adaptive thinking when the field is absent, where Opus 4.8 does not, and thinking tokens are drawn from `MaxTokens`, which a six-round tool chain already fills
- `docker compose` host ports are overridable via `DB_PORT` and `API_PORT`, with an `.env.example` naming the collision each default hits. The stack had never once run on a host with a second Postgres, which silently demoted the whole RAG group of the test plan to the InMemory provider while it still looked green
- `libgssapi-krb5-2` is installed in the aspnet runtime stage, removing the cause of Npgsql's failed Kerberos probe rather than suppressing the symptom
- A manual test plan over all twelve tools, summaries, the dashboard, the sandbox and the worker, tracing every deviation to a file and line and listing what remains untested
- The README describes the current surface instead of the v1.2.0 one

### Known limitations

- The manual pass found six defects this release does not fix, recorded rather than silently carried: #190, #191, #193, #194, #195, #196
- Sonnet 5 follows instructions more literally than Opus 4.8, and reaches for tools less readily with thinking off; the `*.prompt.cs` files are where that is tuned
- Token counts were not re-baselined against `count_tokens` on a real prompt; the shared tokenizer makes it reasoned, not measured
- Still no real auth, no outbox dispatching, no HTTP-level rate limiting
- The port override is documented for `docker compose` only; the `dotnet run` profiles still bind fixed ports

_Full notes: [docs/releases/v1.7.0.md](docs/releases/v1.7.0.md)_

## v1.6.0 — Autonomous Ticket Worker

### Summary

Every earlier release built an assistant that waits to be spoken to; this one lets it work when nobody is there (milestone M8, the last one on the roadmap).
A background loop reviews open tickets on a schedule: it asks for missing information, parks the ticket while it waits, proposes solutions grounded in the knowledge base, and refers every high-impact decision to a person.
It is not a second agent — the tool-calling loop was extracted out of the chat endpoint, so the worker and the assistant run the same loop, the same tools, the same guards and the same command handlers.
Two things differ, and both come from the scope the worker opens per ticket: it audits as `ai-worker` rather than `ai-assistant`, and a review guard holds back the writes a human should have approved.
A refused call comes back to the model as an ordinary error result telling it to comment instead, so the reasoning reaches a person and the ticket is untouched until that person agrees.

### Highlights

- Autonomous ticket worker: `TicketWorker` owns *when* (interval, oldest-first candidates, one bad ticket never ends the loop), `TicketReviewer` owns *what*; off unless `AutonomousWorker:Enabled` says otherwise (ADR 0037)
- The agentic loop is extracted from `AssistantChatService` into `AgentLoop` + `ToolDispatcher`; the chat endpoint becomes an SSE adapter, and a second loop that would drift from the first was deliberately not written
- `HumanReviewGuard` constrains autonomous runs only: reads always pass, `add_comment` passes because it changes nothing, triaging and parking pass because they sort a ticket without finishing it — everything else is refused with the way out named
- New `add_comment` tool for both agents: a write on a ticket that changes no state
- `ai-worker` is a distinct audit actor; both AI actors count as automated through one shared list, so the EF and InMemory dashboard repositories cannot disagree about what "automated" means
- The worker's authority is validated configuration, not a sentence in a prompt; collections bind into an empty list so a narrower policy replaces the defaults rather than being appended to them
- Seven deterministic worker scenarios join the evaluation suite, alongside guard, options-binding and tool-input tests

### Known limitations

- Still no real auth — a busy worker and a busy user share one per-owner rate-limit bucket
- The worker is single-instance by construction: candidate selection takes no lease, so two enabled instances would review the same tickets
- A guard refusal counts as an error on the AI dashboard's per-tool error rate, so a healthy worker raises the error rate of the tools it is not allowed to call
- A proposal reaches a person as a comment, not as an approval queue with its own state; accepting or rejecting it is a human action on the ticket
- The worker never learns from a rejected proposal, and the evaluation suite proves the harness is correct, not that the model's questions are useful

_Full notes: [docs/releases/v1.6.0.md](docs/releases/v1.6.0.md)_

## v1.5.0 — Agent Sandbox, Evaluation Suite & Observability

### Summary

Earlier releases grew what the assistant can do; this one makes it fit to operate (milestone M7).
The agent is fenced in — every tool call passes a guard pipeline before it reaches a handler, with per-owner rate limits, a write budget per turn, input caps, and outright refusal of unknown tool names.
A new evaluation suite drives the real endpoint, loop, guards, and tools against a scripted model, so tool-calling, RAG grounding, and streaming are regression-tested offline.
Runtime behaviour is now observable: a Prometheus scrape endpoint and OpenTelemetry traces expose tool latency, error rates, token usage, retrieval confidence, and a span-level trace of which tools ran and why.
The model still reaches the domain only through the same command handlers, a refused call reads like any validation error, and a number the system cannot measure is reported as unknown rather than as zero.

### Highlights

- Agent sandbox: a `ToolGuardPipeline` at the single admission point where model intent becomes execution; guards split `Check` from `Commit`, so a rate-limit token is never spent on a write another guard refuses (ADR 0035)
- Per-owner token buckets for tool calls and Anthropic round trips, the latter shared by the chat loop and the ticket-summary endpoint; a write budget per turn, argument-size caps, and refusal of unknown tool names, with retrievals never counted against the write budget
- Agent evaluation suite: a new `Tests.Evaluation` project exercises the assistant end to end against a scripted model, faking only the HTTP transport under the Anthropic client — offline, deterministic, no database
- Observability: a `GET /metrics` Prometheus endpoint and OpenTelemetry traces, fed by one decorator over the metrics sink so Prometheus and the AI dashboard cannot drift; tool calls carry an `error` label instead of a pre-computed rate, and each conversation is a span tree from chat to model turn to tool call (ADR 0036)
- Tool-call latency is persisted and shown per tool on the AI Insights page
- Security: pinned `Microsoft.OpenApi` past GHSA-v5pm-xwqc-g5wc, a high-severity advisory pulled in transitively by Swashbuckle

### Known limitations

- Still no real auth — the owner is a constant demo identity, so the per-owner limits are effectively global
- Rate limits and write budgets are per API instance and reset on restart; a multi-instance deployment would need a shared store (ADR 0035)
- The evaluation suite proves the harness is correct, not that the model is good — measuring the model's judgment needs a live model and a different test
- Metrics are per instance and the Prometheus exporter is a pre-release package; traces are exported only when an OTLP endpoint is configured
- Assistant metrics are never pruned, and reset with the process on the InMemory provider

_Full notes: [docs/releases/v1.5.0.md](docs/releases/v1.5.0.md)_

## v1.4.0 — Auto-Routing, Streaming Summaries & AI Insights

### Summary

Turns the assistant from something you talk to into something that works alongside you (milestone M6).
Incoming tickets are triaged on arrival into category, priority, assignee, and status, and an uncertain decision comes back as a suggestion rather than a silent write.
A ticket's history collapses into a structured summary that streams token by token into its own tab.
A new AI Insights page reports what the assistant actually did: automation rate, duplicate-check hit rate, retrieval confidence, per-tool call statistics, and token usage.
The model still reaches the domain only through the same command handlers, every change is audited, and a number the system cannot measure is reported as unknown rather than as zero.

### Highlights

- AI auto-routing: a deterministic `ITicketRouter` classifies category, priority, assignee, and status; `route_ticket` applies it through the existing handlers, and a confidence gate turns an uncertain decision into a suggestion (ADR 0032)
- `TicketCategory` becomes a real domain field, with `Uncategorized` distinct from a ticket deliberately routed to `Other`
- Streaming ticket summaries: `GET /api/v1/tickets/{id}/summary` streams summary, next steps, risks, and missing information into the ticket's `AI Summary` tab; unknowns land in *missing information* instead of being invented (ADR 0033)
- AI operations dashboard: `GET /api/v1/dashboard/ai` and an `AI Insights` page report automation, duplicate rate, retrieval confidence, tool statistics, and token usage over a trailing seven days (ADR 0034)
- Assistant metrics are persisted behind a port on both providers; the sink writes on its own DbContext and swallows its failures, so telemetry can never fail the chat turn it measures
- Every rate is nullable to the wire and renders as `n/a` — an unused system has not achieved 0 % automation
- New assistant tool `route_ticket` (eleven tools total)

### Known limitations

- Still no real auth — the roster is seeded fictitious accounts, not authenticated identities
- Routing is a keyword classifier with a hand-tuned threshold, chosen so triage is always available and reproducible; an embedding or LLM classifier can sit behind the same port later
- Summaries are not persisted and have no rate limiting, so reloading the tab costs a model call
- Assistant metrics are never pruned, and reset with the process on the InMemory provider
- Retrieval confidence needs PostgreSQL + a Voyage key; without them it is reported as unmeasurable rather than low

_Full notes: [docs/releases/v1.4.0.md](docs/releases/v1.4.0.md)_

## v1.3.0 — Knowledge-Base RAG, Hybrid Retrieval & Grounding

### Summary

Grows the optional ticket-only semantic search into a full knowledge system
(milestone M5). The assistant answers how-to questions from a dedicated
knowledge-base corpus and streams the sources it cited; ticket retrieval becomes
hybrid (semantic + keyword fused with metadata filters); and every knowledge-base
answer is grounding-checked so the agent hedges or re-retrieves instead of
asserting an unsupported claim. All optional, degrading honestly without a Voyage
key or on the InMemory provider.

### Highlights

- Knowledge-base RAG: `/KnowledgeBase` corpus (articles, FAQ, internal docs), chunked and embedded by a background worker, with answers that stream cited sources over SSE (ADR 0029)
- Hybrid ticket retrieval: `find_similar_tickets` fuses semantic + keyword via Reciprocal Rank Fusion, with optional status/priority filters and per-result relevance (ADR 0030)
- RAG grounding evaluation: a deterministic `check_grounding` tool scores an answer against its sources; weak grounding makes the agent re-retrieve or hedge (ADR 0031)
- New assistant tools `search_knowledge_base` and `check_grounding` (ten tools total)
- Web: retrieved sources render as a "Sources" card; answers carry a grounding badge
- Build: versions are derived from git tags via MinVer

### Known limitations

- Still no real auth — the roster is seeded fictitious accounts, not authenticated identities
- Knowledge-base RAG, citations, and grounding-against-real-sources require PostgreSQL + a Voyage API key; without them they report unavailable
- The grounding check is lexical, so it assumes an answer and its sources share a language

_Full notes: [docs/releases/v1.3.0.md](docs/releases/v1.3.0.md)_

## v1.2.0 — Semantic Search (RAG) & Agent Roster

### Summary

Broadens the AI assistant into a full read-and-write toolbelt and adds
optional semantic ticket search. The assistant can now search tickets, update
any ticket via find-then-update, change workflow status, and assign work to
seeded roster accounts — all through the same guarded command handlers as the
REST API. Free-text assignees are replaced by a real agent roster across API,
web UI, and assistant.

### Highlights

- Semantic ticket search (RAG) via pgvector + Voyage embeddings, indexed by a background worker (ADR 0024)
- Agent roster with seeded accounts; assignees are real entities, not free text (ADR 0025)
- New assistant tools: `search_tickets`, `change_ticket_status`, `assign_ticket`, and update-any-ticket via find-then-update
- Web: inline edit of ticket fields on the details page (pencil → edit mode)
- Web: comment author chosen from the roster instead of typed free text
- Model-facing text centralized in `*.prompt.cs` partials for easier tuning
- Ticket seeder registered for both persistence providers (InMemory / PostgreSQL parity)

### Known limitations

- Still no real auth — the roster is seeded fictitious accounts, not authenticated identities
- RAG is intentionally minimal (no chunking, hybrid FTS, re-ranking, or vector index tuning) and requires PostgreSQL + a Voyage API key

_Full notes: [docs/releases/v1.2.0.md](docs/releases/v1.2.0.md)_

## v1.1.0

### Summary

Adds an AI intake assistant: users describe an issue in free text and a Claude
model decides via tool calling whether to create or update a ticket, with the
response streamed live to the browser (SSE). Built as an edge adapter — Domain
and Application stay free of any LLM dependency, and AI-driven writes go
through the same command handlers, validation, and audit trail as regular API
requests.

### Highlights

- AI intake assistant with live-streamed chat (SSE)
- LLM tool calling for ticket creation and updates (Claude)
- New `UpdateTicket` use case with validation and audit events
- Tool inputs treated as untrusted input: parsed and guarded before touching the domain
- Model self-correction loop on rejected inputs (bounded iterations)
- Full audit trail for AI actions (actor `ai-assistant`)
- Docker Compose support: assistant enabled via `ANTHROPIC_API_KEY`, boots without it
- ADR 0023 documenting the edge-adapter decision

### Known limitations

- No conversation persistence — transcripts live in the browser session
- No prompt caching, rate limiting, or multi-provider abstraction
- Requires an Anthropic API key; without one the assistant fails gracefully and the rest of the app is unaffected

_Full notes: [docs/releases/v1.1.0.md](docs/releases/v1.1.0.md)_

## v1.0.0

### Summary

ServiceDeskLite 1.0.0 is the first complete release of this reference project.
It includes ticket workflow, comments, audit log, search/paging, a dashboard,
Docker setup, and architecture/API documentation.

### Highlights

- Clean Architecture / Layered Structure
- Ticket workflow with explicit transition rules
- Comments and audit history
- Search, filter and paging
- Dashboard with KPIs
- Docker Compose for local demo setup
- OpenAPI and architecture documentation

### Demo Notes

#### Suggested demo flow

1. Start application
2. Open ticket list
3. Filter/search tickets
4. Open ticket details
5. Change status
6. Add comment
7. Inspect audit history
8. Open dashboard
9. Show API docs / architecture docs

#### What to pay attention to

- Thin API / encapsulated application logic
- Centralized domain rules
- Consistent ProblemDetails error handling
- Traceable workflow behavior
- Clean project structure and documentation

### Included scope

- M1 walking skeleton
- M2 workflow and substance
- M3 polish items required for portfolio readiness

### Known limitations

- Not intended for production use
- Demo/reference project with intentionally limited scope
- Security, auth, and multi-user concerns are only partially addressed or not implemented

### Links

- [Repository](https://github.com/goldbarth/ServiceDeskLite)
- [Documentation / GitHub Pages](https://goldbarth.github.io/ServiceDeskLite/)
- [OpenAPI](https://goldbarth.github.io/ServiceDeskLite/api/openapi)
