# Changelog

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
