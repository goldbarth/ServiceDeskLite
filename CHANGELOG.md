# Changelog

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
