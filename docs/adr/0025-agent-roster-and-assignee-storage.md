# ADR 0025: Agent Roster Entity and Assignee Storage (FK, name snapshot in audit)

## Status

Accepted

## Context and Problem Statement

Ticket assignment was free text: `Assignee` was a `readonly record struct`
wrapping a `Name` string, and `AssignTicketDialog` was a plain text field. There
was no concept of *who* can be assigned — any string was accepted, nothing
validated against a known set of people, and the AI assistant could not assign
at all.

We want a real roster of (fictitious, seeded) agents that both the UI and the
assistant assign against. Introducing an `Agent` entity raises one non-obvious
decision: **how is the assignee stored on the ticket?**

1. **FK** — the ticket stores only `AssignedAgentId`; the display name is
   resolved from the `Agents` table.
2. **Denormalised snapshot** — the ticket stores `AgentId` *and* a copy of the
   name captured at assignment time.

## Decision Drivers

- **Clean Architecture showcase** — a real entity behind the same ports fits the
  project better than a config-only list, and opens the door to future auth.
- **Single source of truth** — an agent's name/active state should live in one
  place, not be duplicated across every ticket that references them.
- **Audit stability** — the audit trail ("Reassigned from X to Y") must remain a
  faithful record of what was true at the time, independent of later renames.
- **Provider parity** — both PostgreSQL and InMemory back the same ports; the
  choice must model cleanly in both.

## Decision Outcome

**The ticket stores an FK (`AssignedAgentId`), and the audit trail snapshots the
agent name at change time.** These are two separate concerns:

- **Current state → FK.** `Ticket.AssignedAgentId : AgentId?` (nullable, null =
  unassigned). The `Agents` table is the single source of truth for name/active
  state; read models resolve the display name by joining to it. `Ticket.Assign`
  no longer carries a name — assignment is by id only.
- **Historical state → name snapshot.** `AssigneeChangedDomainEvent` still
  carries the previous/new **names** (resolved by the handler from the roster at
  the moment of change), and the audit payload persists them. A later rename
  therefore does not rewrite history, giving audit stability *without*
  denormalising onto the live ticket row.

Validation moves into `AssignTicketHandler`: the target agent must exist and be
`Active` (else the change is rejected as a value the caller — user or model —
can act on). The "cannot assign a closed ticket" domain rule is preserved.

### Why not a snapshot on the ticket

Denormalising the name onto the ticket would duplicate mutable state and risk the
live ticket drifting from the roster. The only thing a snapshot buys — audit
stability — is already provided by capturing the name in the immutable audit
event. So the live row stays a clean FK; the snapshot lives where history is
supposed to live.

## Consequences

- New `Agent` aggregate (`AgentId`, `Name`, `Email`, `Active`) with its own EF
  mapping + migration and InMemory parity; a fixed roster is seeded for both
  providers, alongside the ticket seeder.
- Read repositories resolve the assignee name via the roster (join on Postgres,
  lookup in InMemory) when projecting list/detail DTOs.
- `GET /api/v1/agents` exposes the active roster; the UI assign dialog and the
  assistant `assign_ticket` tool both resolve agents through it.
- The assistant's system prompt names the active roster per request (issue #193),
  so the model knows the valid names before it guesses one; the failed-call error
  path of `assign_ticket` remains only as a fallback. Injection scales with the
  demo-sized roster — if the roster ever outgrows a prompt line, the replacement
  is a `list_agents` retrieval tool (catalog entry, `*.prompt.cs` pair), not a
  longer prompt.
- Still fully fictitious — no real accounts, no login (consistent with ADR 0022).
