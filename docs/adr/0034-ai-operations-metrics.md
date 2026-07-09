# ADR 0034: AI Operations Metrics (persisted assistant telemetry, honest empty states)

## Status

Accepted

## Context and Problem Statement

Issue #161 asks for a dashboard over AI operations: ticket volume, automation rate, duplicate
rate, RAG confidence, tool-calling statistics, and token usage.

The metrics split into two families with entirely different provenance.

**Already recorded.** Ticket volume and automation rate are derivable from data the system
already keeps. Every write path produces an audit event, and the assistant reaches the domain
only through the same command handlers as the UI and the API (ADR-0023). Its actions are
therefore audited like anyone else's, and `Actor` is the whole difference between an automated
action and a human one.

**Recorded nowhere.** Tool calls, token usage, duplicate hits and retrieval confidence existed
only for the lifetime of a request. Confidence was written to a log line; token usage was never
read off the API response at all.

Decisions:

1. **Where do assistant metrics live**, given that the assistant is an edge adapter and the
   dashboard is a read model?
2. **How does telemetry write without endangering the thing it observes?**
3. **What does the dashboard show when a metric has no data behind it?**

## Decision Drivers

- **A metrics write must never fail a chat turn.** Telemetry is subordinate to the feature it
  measures. If the two ever compete, the feature wins.
- **Both persistence providers must agree.** `Tests.EndToEnd` runs the full stack against
  Postgres and InMemory, and a figure that differs between them is a bug in one of them.
- **An unmeasured value is not zero.** "0 % automation" and "nobody has used the assistant" are
  different statements, and a dashboard that conflates them misleads the person reading it.
- **The edge owns its vocabulary.** Tool names belong to the Anthropic adapter. Reporting must
  not depend on recognising one.

## Decision Outcome

**1. A persisted sink behind an application port.**

`IAssistantMetricsSink` records two append-only facts: an `AssistantToolInvocation` per tool
call, and an `AssistantTokenUsage` per model round trip. `IAiDashboardRepository` reads them
back, aggregated. Both ports have a Postgres and an InMemory implementation, matching the
swappable-persistence rule.

Two ports rather than one, because they change for different reasons: the sink follows what the
assistant does, the repository follows what the dashboard asks.

Rejected: in-process counters (`System.Diagnostics.Metrics`). No migration and no write path,
but the numbers would reset on restart and count only one instance, and the dashboard would have
to explain that in every panel. The question "how much of last week's work was automated?" has
no answer in a counter that started this morning.

**2. The sink writes on its own DbContext, and swallows its failures.**

`EfAssistantMetricsSink` resolves a `ServiceDeskLiteDbContext` from a fresh scope per write.
Sharing the request's context would let a metrics `SaveChanges` flush whatever that context is
tracking: a command that staged entities and then failed before saving would get its partial
write committed by a telemetry call. That is a corruption path through code whose only job is to
observe.

Every write is wrapped in a catch that logs and continues. A dropped metric costs a dashboard
row; a thrown one costs the user's chat turn.

**3. Tools are classified at the edge, not recognised by name downstream.**

Each invocation carries an `AssistantToolKind` — `Action`, `Retrieval`, `DuplicateCheck`, or
`Evaluation` — assigned next to the tool dispatch in `AssistantChatService`. The duplicate rate
counts `DuplicateCheck` invocations; renaming `find_similar_tickets` stays an edge concern.

`Evaluation` exists to keep `check_grounding` out of the retrieval confidence average. Its score
grades an answer against retrieved passages; a retrieval's score ranks evidence. Averaging the
two produces a number that measures nothing.

**4. Every rate is nullable, all the way to the wire.**

`AutomationDto.Rate`, `RetrievalDto.DuplicateRate` and `RetrievalDto.AverageConfidence` return
null when their denominator is zero, and the contract carries the null to the client. The UI
renders `n/a`, never `0 %`.

Three retrieval states are kept distinct, because collapsing any two of them misinforms:

- semantic search is not configured (no Voyage key, or the InMemory provider) — not measurable;
- it is configured but nothing scored yet — measurable, no samples;
- it scored — a real average.

A keyword-only retrieval still reports its match count while withholding a confidence score, so
the duplicate rate counts a hit the confidence average must not.

**5. Aggregation lives in the application layer.**

`AiDashboardAggregation` turns raw invocations into retrieval and per-tool figures. Both
adapters fetch the window and delegate; neither computes a rate. Duplicating that arithmetic in
two providers is exactly how they would come to disagree.

## Consequences

- Two new tables, `AssistantToolInvocations` and `AssistantTokenUsages`, and one write per tool
  call plus one per model turn. Both are append-only and are never read on a hot path.
- The window is a fixed trailing seven days, matching the ticket dashboard's "resolved (7d)".
  It is not a query parameter; the reported `windowDays` is derived from the range the numbers
  actually cover, so it cannot drift from them.
- Metrics are never deleted. The tables grow with assistant usage; retention is not addressed
  here and would need its own decision.
- The InMemory provider keeps metrics in process memory, so they vanish on restart. That matches
  how it already treats tickets.
- Seeded demo tickets produce no audit events, so a fresh deployment shows a large ticket volume
  next to zero audited actions. That is accurate, if briefly surprising.
- Token usage is read from the final `message_delta`, the only place the API states it. A turn
  the client abandons mid-stream is not counted.

## Related

- ADR 0023: the assistant acts only through application-layer command handlers, which is what
  makes the audit actor a trustworthy automation signal.
- ADR 0028: retrieval confidence as a signal, and honest degradation when the semantic half is
  unavailable.
- ADR 0031: `check_grounding` and the grounding score kept apart from retrieval relevance here.
