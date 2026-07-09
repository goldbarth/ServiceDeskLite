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

5. **AI Insights** — after the assistant has run, this page reports the last 7 days: automation rate, duplicate-check hit rate, retrieval confidence, per-tool call counts, and token usage. On a fresh start it shows `n/a` rather than `0 %` for every rate, because nothing has been measured yet.

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
| GET    | `/api/v1/tickets/{id}/summary` | AI ticket summary, streams SSE |
| GET    | `/api/v1/dashboard/summary` | Dashboard KPIs |
| GET    | `/api/v1/dashboard/ai` | AI operations metrics (trailing 7 days) |
| POST   | `/api/v1/assistant/chat` | AI assistant, streams SSE |
| GET    | `/metrics` | Prometheus scrape (no API key — see Observability) |

Errors follow RFC 9457 ProblemDetails with machine-readable `code` fields.

### Assistant endpoint

`POST /api/v1/assistant/chat` takes one new user message plus an optional `conversationId` (conversation state is persisted server-side, ADR 0026) and streams Server-Sent Events. Omit `conversationId` to start a new conversation; the server returns the id on the `conversation` event, which the client resends on the next turn:

```json
{
  "conversationId": null,
  "newMessage": { "role": "User", "content": "Der Drucker im 3. Stock ist ausgefallen. Bitte bis Freitag 9 Uhr beheben." }
}
```

Event stream: `conversation` (id for the next turn) → `text` (response deltas) → `tool_call` / `tool_result` (with `ticketId`) → `citation` (knowledge-base sources, when the answer draws on the KB) → `done`; failures arrive as an `error` event. The model has twelve tools — `add_comment` (write a comment on a ticket: a follow-up question, a proposed solution, or the reasoning behind an action), `create_ticket`, `update_ticket`, `change_ticket_status` (workflow-validated status changes), `assign_ticket` (assign/reassign/unassign against the seeded agent roster), `route_ticket` (deterministic auto-triage of a ticket into category/priority/assignee/status, applied through the update/assign/change-status handlers when confident and returned as a suggestion when not — ADR 0032), `search_tickets` (find existing tickets by filter), `find_similar_tickets` (hybrid dedup — semantic + keyword fused via RRF, with optional status/priority filters), `search_knowledge_base` (semantic retrieval over the KB corpus for cited how-to answers; requires a Voyage key + Postgres, otherwise reports unavailable and cites nothing), `check_grounding` (deterministic grounding score of a drafted answer against retrieved passages, so the agent hedges or re-retrieves on weak support — ADR 0031), and the long-term memory pair `remember` / `recall_memory` (store and recall durable user facts across conversations; requires a Voyage key + Postgres, otherwise reports unavailable) — all executing through the regular application-layer handlers.

The model can chain these tools autonomously in a single turn (e.g. dup-check → create → assign, bounded by `Anthropic:MaxToolIterations`, default 6). Transient tool failures (rate limits, upstream 5xx, timeouts) are retried at the edge with bounded backoff (`Anthropic:MaxToolRetries`, `Anthropic:ToolRetryBaseDelayMs`) before surfacing as an error `tool_result`; deterministic failures surface immediately for the model to correct (ADR 0027).

Retrieval tools attach a confidence signal (top-match relevance) to their result, emitted on the `tool_result` SSE event (`confidence`) and logged. `find_similar_tickets` blends semantic + keyword signals via Reciprocal Rank Fusion with optional status/priority filters (ADR 0030); when the semantic signal is unavailable (no Voyage key / InMemory) it degrades to keyword-only, labelled as weaker evidence, and the prompt tells the model to re-plan on weak/empty/contradictory results (ADR 0028).

### Ticket summary endpoint

`GET /api/v1/tickets/{id}/summary` streams a structured, read-only summary of one ticket as Server-Sent Events (ADR 0033).
It is a single model call with no tools — the summary informs an agent, it never changes a ticket — and is deliberately not part of the assistant's tool list.
An unknown ticket id returns a 404 ProblemDetails before any stream is opened.

Event stream: `delta` (a chunk of text, tagged with its section) → `done`; failures arrive as an `error` event.
Sections are `Summary`, `NextSteps`, `Risks`, and `MissingInfo`:

```
event: delta
data: {"section":"Summary","text":"This is a critical-priority ticket about a"}

event: delta
data: {"section":"NextSteps","text":"- Assign the ticket to an agent"}

event: done
data: {}
```

The model emits marker-delimited sections (`<<SUMMARY>>`, `<<NEXT_STEPS>>`, `<<RISKS>>`, `<<MISSING_INFO>>`) which the API recovers from the token stream and never forwards to the client.
Output is capped by `Anthropic:SummaryMaxTokens` (default 2048).
The web client streams the summary on first open of the ticket's `AI Summary` tab and keeps it for the lifetime of the page; there is no server-side caching.
Each summary spends one model turn from the caller's sandbox budget (see below), so a client that reloads the tab in a loop is throttled rather than billed.

### Agent sandbox

Every tool call passes a guard layer before it reaches a command handler (ADR 0035).
A refused call comes back to the model as an ordinary `tool_result` with `is_error: true` and the reason, so the model adapts instead of the request failing.

| Setting | Default | What it bounds |
|---------|---------|----------------|
| `AgentSandbox:MaxInputCharacters` | 16384 | Raw JSON size of one tool's arguments |
| `AgentSandbox:MaxStringCharacters` | 8000 | Any single string inside those arguments |
| `AgentSandbox:MaxWritesPerTurn` | 6 | Ticket- or memory-changing tool calls per chat turn |
| `AgentSandbox:ToolCallsPerMinute` | 60 | Tool calls per owner |
| `AgentSandbox:ModelTurnsPerMinute` | 30 | Anthropic round trips per owner, chat and summaries combined |

Unknown tool names are refused outright.
Retrievals never count against the write budget, so a model that has spent it can still read.

The buckets live in the API process, keyed by `ICurrentUser.Owner` — today a single demo owner, so the limits are effectively global.
They reset when the API restarts, and a multi-instance deployment would need a shared store.
Exhausting the model-turn budget ends the stream with an `error` event rather than an HTTP 429, because the response has already begun streaming by the time the model is called.

To see it work, start the API with a tightened limit and ask the assistant to create two tickets:

```bash
AgentSandbox__MaxWritesPerTurn=1 dotnet run --project src/ServiceDeskLite.Api
```

The first `create_ticket` succeeds; the second comes back as an error `tool_result`, and the assistant tells the user which ticket it did not create.

### Autonomous ticket worker

A background loop that reviews open tickets on a schedule (ADR 0037): it asks for missing information, proposes grounded solutions, and refers every high-impact decision to a person.
It runs the same agent loop, the same tools and the same command handlers as the chat assistant.
Two things differ, and both come from the scope it opens per ticket: it audits as **`ai-worker`** rather than `ai-assistant`, and the review guardrail holds back the writes a human should have approved.

**It is off by default.** A service that starts writing to tickets the moment it boots is not something to get without asking for it:

```bash
AutonomousWorker__Enabled=true dotnet run --project src/ServiceDeskLite.Api
```

| Setting | Default | What it controls |
|---------|---------|------------------|
| `AutonomousWorker:Enabled` | `false` | Whether the worker runs at all |
| `AutonomousWorker:ScanIntervalSeconds` | `300` | Time between scans |
| `AutonomousWorker:MaxTicketsPerRun` | `5` | Tickets reviewed per scan |
| `AutonomousWorker:MinTicketAgeMinutes` | `15` | How settled a ticket must be before it is touched |
| `AutonomousWorker:ScanStatuses` | `New, Triaged, InProgress` | Which tickets are eligible |
| `AutonomousWorker:AutonomousWrites` | `add_comment` | State-changing tools it may run unattended |
| `AutonomousWorker:AutonomousStatusTransitions` | `Triaged, Waiting` | Status changes it may apply unattended |

**What it may do on its own.** Read anything. Comment on a ticket — that is how it reaches a person, and it changes nothing. Triage a new ticket, and park a ticket in `Waiting` once it has asked a question.

**What it may not.** Close, resolve, assign, update or route a ticket, or open a new one. Those come back to the model as a refusal that names the way out: post the proposal as a comment, and do not retry. The model complies, the reasoning lands on the ticket, and a person decides. Nothing is written until they do.

`Waiting` is deliberately not scanned — the worker moves a ticket there when it needs an answer, and scanning it again would mean asking the same question twice. Tickets are reviewed oldest first, so none starves behind newer arrivals.

Widening its authority is a configuration change, not a code change. To let it resolve tickets too:

```bash
AutonomousWorker__Enabled=true \
AutonomousWorker__AutonomousStatusTransitions__0=Triaged \
AutonomousWorker__AutonomousStatusTransitions__1=Waiting \
AutonomousWorker__AutonomousStatusTransitions__2=Resolved \
dotnet run --project src/ServiceDeskLite.Api
```

The worker spends the same per-owner sandbox budgets as the chat assistant, and its runs appear in the traces (`worker.scan`, `worker.ticket`) and metrics from ADR 0036; the `agent.mode` span tag tells the two agents apart. A guard refusal is recorded like any other error result, so a healthy worker raises the *error rate* of the tools it is not allowed to call — nothing was written, and the AI dashboard reports it as an error nonetheless.

### AI dashboard endpoint

`GET /api/v1/dashboard/ai` returns assistant metrics over a trailing 7-day window (ADR 0034):
ticket volume, automation rate, duplicate-check hit rate, retrieval confidence, per-tool call
statistics, and token usage.
The web UI renders it at **`http://localhost:5310/ai-insights`** ("AI Insights" in the navigation).

Two figures come from data the system already keeps: ticket volume from the tickets themselves, and the automation rate from audit events whose actor is a model — `ai-assistant` in a conversation, `ai-worker` on a background scan.
The rest is captured as the assistant runs: one record per tool call, one per model turn.
A metrics write that fails is logged and dropped; it never fails the chat turn it was measuring.

**Rates are `null`, not `0`, when nothing backs them.** The UI renders these as `n/a`:

```json
{
  "windowDays": 7,
  "automation": { "aiActions": 0, "totalActions": 0, "rate": null },
  "retrieval": { "duplicateChecks": 0, "duplicateRate": null, "averageConfidence": null, "semanticAvailable": false },
  "tools": [],
  "tokens": { "modelTurns": 0, "inputTokens": 0, "outputTokens": 0, "totalTokens": 0 }
}
```

`semanticAvailable: false` means this deployment has no Voyage key or no pgvector, so retrieval confidence is not measurable at all — distinct from a measured low score.
A duplicate check that ran keyword-only still reports its matches, so the duplicate rate stays meaningful where confidence is not.

On the InMemory provider the metrics live in process memory and reset when the API restarts; on Postgres they are persisted (tables `AssistantToolInvocations`, `AssistantTokenUsages`) and are never pruned.
Seeded demo tickets carry no audit events, so a fresh deployment shows many tickets next to zero audited actions.

### Observability (Prometheus + tracing)

The same assistant signals the AI dashboard aggregates are also emitted as OpenTelemetry metrics and traces (ADR 0036).
The dashboard answers "how is the assistant doing" on a page; these answer an operator's questions — alert on an error rate, graph token spend over a week, open one slow conversation.
One recording point feeds both, so they cannot drift.

`GET /metrics` exposes the Prometheus scrape and **needs no API key** — a scraper is infrastructure, not a client, and the endpoint carries only aggregate counters, no ticket or conversation content:

```bash
curl http://localhost:5300/metrics | grep '^servicedesklite'
```

| Setting | Default | What it controls |
|---------|---------|------------------|
| `Observability:PrometheusEnabled` | `true` | Whether `/metrics` is mapped |
| `Observability:MetricsPath` | `/metrics` | Scrape path (also the API-key bypass path) |
| `Observability:ServiceName` | `servicedesklite-api` | `service.name` resource attribute |
| `Observability:OtlpEndpoint` | *(empty)* | OTLP trace collector; spans are exported only when set |

Instruments (all prefixed `servicedesklite_assistant_`):

| Metric | Type | Labels |
|--------|------|--------|
| `tool_calls_total` | counter | `tool`, `kind`, `error` |
| `tool_duration_milliseconds` | histogram | `tool`, `kind`, `error` |
| `retrieval_confidence` | histogram (0..1) | `tool`, `kind` — recorded only for retrieval/dup-check kinds |
| `model_turns_total` | counter | `model` |
| `tokens_total` | counter | `model`, `direction` (`input`/`output`) |

There is deliberately no error-rate metric: a ratio recorded at record time cannot be re-aggregated across windows.
The call counter carries an `error` label instead, and the rate is a query-time division. Error rate per tool over five minutes:

```promql
sum by (tool) (rate(servicedesklite_assistant_tool_calls_total{error="true"}[5m]))
/
sum by (tool) (rate(servicedesklite_assistant_tool_calls_total[5m]))
```

Tracing is off until `Observability:OtlpEndpoint` points at a collector; the spans always exist and cost almost nothing without a listener.
The trace of one conversation is an `assistant.chat` span with an `assistant.model_turn` child per Anthropic round trip and an `assistant.tool` child per tool call — the tool span carries the tool, its kind, whether it errored, the duration, the retrieval confidence, the ticket it touched, and, on a guard refusal, the guard and its reason (span status set to error).
That is the "which tools, why" trail: a write is traceable to the ticket it changed, a refusal to the rule that refused it.

### Tests

```bash
dotnet test
```

Runs all suites (Domain, Application, API, Integration, Web, EndToEnd, Evaluation) — no database or API key required; test hosts inject fakes.

`Tests.Evaluation` is the agent evaluation suite: it drives the real assistant endpoint, loop, guards, tools and handlers against a scripted model, replacing only the HTTP transport under the Anthropic client.
It pins tool-calling, RAG grounding and streaming behaviour, and it needs no network.
See [testing/overview.md](../testing/overview.md) for what it does and does not evaluate.

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
