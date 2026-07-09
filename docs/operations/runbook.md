# Runbook

How to get ServiceDeskLite running locally in under five minutes. Written for reviewers and tech leads — the first section covers the web UI (the fastest way to see everything working), the second documents the API surface.

## Quick reference

| Service | URL                     | Started with                                        |
|---------|-------------------------|-----------------------------------------------------|
| API     | `http://localhost:5300` | `dotnet run --project src/ServiceDeskLite.Api`      |
| Web     | `http://localhost:5310` | `dotnet run --project src/ServiceDeskLite.Web`      |
| Swagger | `http://localhost:5300/swagger` | included in the API (Development only)      |

| Config | Value | Where |
|--------|-------|-------|
| Persistence (Development) | InMemory — no database needed | `src/ServiceDeskLite.Api/appsettings.Development.json` |
| API key header | `X-Api-Key: dev-api-key-not-a-secret` | both `appsettings.Development.json` files |
| Anthropic API key | user-secrets, **required** (see setup) | `dotnet user-secrets` on the API project |
| Assistant timezone | `Anthropic:UserTimeZone`, default `Europe/Berlin` | API options |
| Voyage API key | user-secrets, *optional* — enables semantic ticket search (RAG) | `dotnet user-secrets` on the API project |

## 1. Run the web UI

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) — verify with `dotnet --version` (must be 10.x)
- An Anthropic API key for the AI assistant — create one at [platform.claude.com](https://platform.claude.com) (Settings → API Keys; new accounts need a small credit top-up)

### One-time setup

The API validates the Anthropic key at startup and refuses to boot without it:

```bash
cd src/ServiceDeskLite.Api
dotnet user-secrets set Anthropic:ApiKey sk-ant-YOUR-KEY
cd ../..
```

The key lives in `~/.microsoft/usersecrets/`, never in the repository.

Optional — semantic ticket search (RAG): the assistant can check for duplicate
tickets via embeddings (Voyage AI + pgvector). This needs the Postgres
provider (Docker setup below) and a [Voyage AI key](https://dashboard.voyageai.com)
(free tier is plenty). Without the key, or on the InMemory provider, the
assistant simply works without the duplicate check:

```bash
dotnet user-secrets set Voyage:ApiKey pa-YOUR-KEY --project src/ServiceDeskLite.Api
```

### Start

Two terminals from the repository root:

```bash
# Terminal 1 — API (InMemory persistence, seeded demo data)
dotnet run --project src/ServiceDeskLite.Api

# Terminal 2 — Web
dotnet run --project src/ServiceDeskLite.Web
```

Open **`http://localhost:5310`** (explicitly `http://` — the default launch profiles do not bind the HTTPS ports).

### Suggested demo flow

1. **Dashboard** — KPI overview of the seeded ticket queue.
2. **Tickets / Board** — list with filtering, sorting, paging; Kanban board with status transitions.
3. **Assistant** (the AI intake) — type a free-text issue, e.g.:
   > *"Der Drucker im 3. Stock reagiert seit heute Morgen nicht mehr, mehrere Kollegen sind betroffen. Bitte bis Freitag morgens beheben."*
   - The response streams live (SSE). Because "morgens" is vague, the assistant asks for a concrete time instead of guessing.
   - Answer e.g. *"8 Uhr"* — the model calls the `create_ticket` tool; a green chip links to the created ticket.
   - Follow up with *"Setze die Priorität auf Critical"* — the model calls `update_ticket` on the ticket it just created.
   - It can also edit a ticket it did *not* create in this conversation: ask *"Setze das Login-Ticket auf hohe Priorität"* — the model resolves the description via `search_tickets`, then calls `update_ticket` on the matched id (and asks which one if several match).
4. **Ticket details** — open the created ticket: due date matches local time, and the audit history shows `ticket.created` / `ticket.details_updated` events with actor `ai-assistant`. The pencil icon in the header opens an inline edit form (title, description, priority, due date); Save PATCHes only the changed fields and refreshes the ticket, Cancel discards. Editing is disabled for closed tickets (domain rule); status and assignee keep their dedicated dialogs. In the Comments tab the reply author is picked from the seeded agent roster (no real login yet — in production this would be the signed-in user).

Everything the assistant does runs through the same command handlers as the UI and API — validation, audit trail, and outbox apply unchanged.

## 2. API

All endpoints require the `X-Api-Key` header (Development value: `dev-api-key-not-a-secret`). Interactive documentation: Swagger at `http://localhost:5300/swagger`. Ready-made requests: `src/ServiceDeskLite.Api/ServiceDeskLite.Api.http` (runnable from Rider/VS Code).

### Endpoints

| Method | Path | Purpose |
|--------|------|---------|
| GET    | `/api/v1/tickets` | Search tickets (filter, sort, paging) |
| POST   | `/api/v1/tickets` | Create ticket |
| GET    | `/api/v1/tickets/{id}` | Ticket details incl. conversation |
| PATCH  | `/api/v1/tickets/{id}` | Partial update (title, description, priority, due date) |
| POST   | `/api/v1/tickets/{id}/status` | Change status (workflow-validated) |
| POST   | `/api/v1/tickets/{id}/assign` | Assign / unassign |
| POST   | `/api/v1/tickets/{id}/comments` | Add comment |
| GET    | `/api/v1/tickets/{id}/audit-events` | Audit history |
| GET    | `/api/v1/dashboard/summary` | Dashboard KPIs |
| POST   | `/api/v1/assistant/chat` | AI assistant, streams SSE |

Errors follow RFC 9457 ProblemDetails with machine-readable `code` fields.

### Assistant endpoint

`POST /api/v1/assistant/chat` takes one new user message plus an optional `conversationId` (conversation state is persisted server-side, ADR 0026) and streams Server-Sent Events. Omit `conversationId` to start a new conversation; the server returns the id on the `conversation` event, which the client resends on the next turn:

```json
{
  "conversationId": null,
  "newMessage": { "role": "User", "content": "Der Drucker im 3. Stock ist ausgefallen. Bitte bis Freitag 9 Uhr beheben." }
}
```

Event stream: `conversation` (id for the next turn) → `text` (response deltas) → `tool_call` / `tool_result` (with `ticketId`) → `citation` (knowledge-base sources, when the answer draws on the KB) → `done`; failures arrive as an `error` event. The model has eleven tools — `create_ticket`, `update_ticket`, `change_ticket_status` (workflow-validated status changes), `assign_ticket` (assign/reassign/unassign against the seeded agent roster), `route_ticket` (deterministic auto-triage of a ticket into category/priority/assignee/status, applied through the update/assign/change-status handlers when confident and returned as a suggestion when not — ADR 0032), `search_tickets` (find existing tickets by filter), `find_similar_tickets` (hybrid dedup — semantic + keyword fused via RRF, with optional status/priority filters), `search_knowledge_base` (semantic retrieval over the KB corpus for cited how-to answers; requires a Voyage key + Postgres, otherwise reports unavailable and cites nothing), `check_grounding` (deterministic grounding score of a drafted answer against retrieved passages, so the agent hedges or re-retrieves on weak support — ADR 0031), and the long-term memory pair `remember` / `recall_memory` (store and recall durable user facts across conversations; requires a Voyage key + Postgres, otherwise reports unavailable) — all executing through the regular application-layer handlers.

The model can chain these tools autonomously in a single turn (e.g. dup-check → create → assign, bounded by `Anthropic:MaxToolIterations`, default 6). Transient tool failures (rate limits, upstream 5xx, timeouts) are retried at the edge with bounded backoff (`Anthropic:MaxToolRetries`, `Anthropic:ToolRetryBaseDelayMs`) before surfacing as an error `tool_result`; deterministic failures surface immediately for the model to correct (ADR 0027).

Retrieval tools attach a confidence signal (top-match relevance) to their result, emitted on the `tool_result` SSE event (`confidence`) and logged. `find_similar_tickets` blends semantic + keyword signals via Reciprocal Rank Fusion with optional status/priority filters (ADR 0030); when the semantic signal is unavailable (no Voyage key / InMemory) it degrades to keyword-only, labelled as weaker evidence, and the prompt tells the model to re-plan on weak/empty/contradictory results (ADR 0028).

### Tests

```bash
dotnet test
```

Runs all suites (Domain, Application, API, Integration, Web, EndToEnd) — no database or API key required; test hosts inject fakes.

### PostgreSQL instead of InMemory (optional)

```bash
docker compose up --build
```

Starts API + PostgreSQL (with pgvector) on `http://localhost:8080` (migrations apply automatically). The web frontend must still be started locally; point it at the Docker API with `ApiClient__BaseUrl=http://localhost:8080 dotnet run --project src/ServiceDeskLite.Web`. To use the AI assistant in this setup, export `ANTHROPIC_API_KEY` before `docker compose up` — without it the API boots with a placeholder and assistant requests fail gracefully. Additionally export `VOYAGE_API_KEY` to enable semantic ticket search: a background worker then embeds all (seeded and new) tickets, and the assistant checks for duplicates via the `find_similar_tickets` tool before creating a ticket (see ADR 0024). The same key also enables knowledge-base RAG: a second worker embeds the `/KnowledgeBase` corpus into pgvector, and the assistant answers how-to questions via `search_knowledge_base`, streaming cited sources (see ADR 0029).

## Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| API exits at startup: `Anthropic:ApiKey is not configured` | Missing user-secret | Run the one-time setup above |
| Browser: connection refused on `:7238` / `:7023` | HTTPS ports aren't bound by the default profiles | Use `http://localhost:5310` / `:5300` |
| API returns 401 | `X-Api-Key` header missing/wrong | Development key: `dev-api-key-not-a-secret` |
| Assistant shows "The AI service is currently unavailable" | Invalid Anthropic key, no credit, or no network | Check the key at platform.claude.com; API log has details |
| Created tickets disappear after restart | InMemory persistence is per-process | Expected in Development; use the PostgreSQL setup for durability |
| Assistant says semantic search is unavailable | InMemory provider, or `Voyage:ApiKey` not set | Use the PostgreSQL setup and set the Voyage key (optional feature) |
| Log: `Ticket embedding batch failed` | Invalid Voyage key or no network | Check the key at dashboard.voyageai.com; worker retries next poll |
