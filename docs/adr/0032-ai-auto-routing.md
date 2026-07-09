# ADR 0032: AI Auto-Routing (deterministic triage applied through command handlers)

## Status

Accepted

## Context and Problem Statement

Tickets arrive as free text and are then triaged by hand: someone picks a priority,
decides what kind of issue it is, assigns an owner, and moves it out of `New`.
Issue #159 asks the assistant to do this automatically — derive a priority, category,
assignee and status from the ticket content — while keeping every change auditable and
never silently committing an uncertain guess.

The ticket aggregate had priority and status but **no category** (deliberately deferred
in #157). Category detection is now in scope, so this is also a schema decision.

Decisions:

1. **What classifies** the ticket — an LLM, or a deterministic rule set?
2. **What triggers** routing, and how is the decision **applied** without bypassing the
   domain rules and audit trail?
3. **How is "category"** modelled?
4. **What happens when the classifier is unsure?**

## Decision Drivers

- **Auditable through the existing handlers** (acceptance): every change must raise a
  domain event → audit record, exactly as a human edit would.
- **Deterministic and fixture-testable**, consistent with the grounding evaluator
  (ADR 0031) — routing logic must be reproducible in the test suite with no model call.
- **Edge-adapter invariant (ADR 0023):** the model decides *what*, but may only act
  through the same application-layer command handlers as the REST API; no business logic
  in the LLM path, no LLM in the Application/Domain layers.
- **Honest under uncertainty:** a weak decision is a suggestion, not an action.

## Decision Outcome

**Classifier: a deterministic, rule-based `ITicketRouter`** (`KeywordTicketRouter`).
It matches keywords over the title + description to a `TicketCategory` and an urgency
`TicketPriority`, maps the category to a roster owner, and suggests the triage status
move (`New` → `Triaged`). It is a pure function returning a `RoutingDecision`
(category, priority, assignee, status, **confidence**, rationale) — no I/O, unit-tested
against fixtures. An embedding/LLM classifier could later sit behind the same port; the
default is rule-based so routing is always available and reproducible.

**Application: a `RouteTicketHandler` use-case that owns the confidence gate and applies
the decision through the existing command handlers** — `UpdateTicketHandler` (priority +
category), `AssignTicketHandler`, `ChangeTicketStatusHandler`. There is no direct domain
write, so routing inherits their validation, the closed-ticket rules, and the audit trail
(each raises a domain event → audit record, actor `ai-assistant`). A status move is only
attempted when it is a valid transition from the current state; a rejected sub-step is
skipped, not fatal.

**Trigger: a `route_ticket` assistant tool.** The prompt directs the model to call it
right after `create_ticket`. This keeps routing on the edge-adapter path and reuses the
autonomous multi-step chaining (ADR 0027: dup-check → create → route). The deterministic
router is also reusable by a future server-side/REST trigger without change.

**Category: a real `TicketCategory` domain field**, defaulting to `Uncategorized`
(value 0, so existing rows backfill with no data migration). `Uncategorized` means "not
yet routed", distinct from a ticket deliberately routed to `Other`. It is stored as an
int like priority/status, changed only via `UpdateDetails` (audited), and surfaced through
the DTOs and the web ticket list + detail views.

**Uncertainty: a confidence threshold (0.5).** Confidence starts at a base and rises when
a category and/or an urgency cue actually matched. Below the threshold — e.g. content that
matched neither — `RouteTicketHandler` applies nothing and returns the decision as a
**suggestion**; the tool relays it and the prompt tells the model to confirm with the user
or set the fields explicitly, rather than committing an uncertain triage.

## Consequences

- **Positive:** incoming tickets are triaged automatically; every applied change is
  audited through the same handlers as a human edit; the router is deterministic and
  fixture-tested; low-confidence cases degrade to an explicit suggestion; category is a
  first-class, persisted, provider-parity field verified end-to-end on both providers.
- **Negative / limitations:** the rules are **lexical and English-tuned** — a
  non-English ticket, or one using vocabulary outside the keyword sets, routes to
  `Other`/`Medium` with low confidence (correctly suggested, not applied). The
  category→owner map is a static demo mapping (agents carry no specialty attribute), and
  each applied change is its own transaction rather than one atomic routing commit.
- **Deliberately cut:** an LLM/embedding classifier, a server-side auto-trigger on
  ticket creation (the outbox dispatcher is still stubbed, ADR 0021), agent-specialty
  modelling, and a hard gate that would route without the model in the loop.

## Related

- ADR 0023 - AI Assistant as Edge Adapter (act only through command handlers)
- ADR 0027 - Autonomous multi-step tool chains (create → route in one turn)
- ADR 0031 - RAG Grounding Evaluation (the deterministic, fixture-tested precedent)
- ADR 0025 - Agent assignment FK model (the roster routing assigns against)
- `src/ServiceDeskLite.Application/Tickets/Routing/KeywordTicketRouter.cs`
- `src/ServiceDeskLite.Application/Tickets/Routing/RouteTicketHandler.cs`
- `src/ServiceDeskLite.Api/Assistant/RouteTicketTool.cs`
