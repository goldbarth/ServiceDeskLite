# ADR 0036: Observability (Prometheus metrics and decision tracing for the assistant)

## Status

Accepted.

## Context and Problem Statement

Issue #164 asks for the agent's behaviour to be observable in production: tool latency, error
rates, token usage, RAG confidence, and a trace of which tools ran and why.

ADR 0034 already records these signals once, through `IAssistantMetricsSink`, and aggregates them
into the AI dashboard.
That dashboard answers "how is the assistant doing" for a person reading a page.
It does not answer an operator's questions: alert when the tool error rate crosses a threshold,
graph token spend per model over a week, or open one slow conversation and see the exact tool call
that hung.
Those need a metrics backend scraping a well-known endpoint and a distributed trace, not a Blazor
page backed by a seven-day SQL window.

Decisions:

1. **How does a second consumer read the same signals without a second recording point** that can
   drift from the first?
2. **What carries the "which tools, why" trace**, and how is it kept from coupling to a backend?
3. **What does the scrape endpoint expose**, and does it sit inside or outside the API-key guard?

## Decision Drivers

- **One recording point.** The dashboard and Prometheus must see the same numbers. A new signal
  cannot reach one and miss the other, which is exactly how two call sites drift apart.
- **The instruments carry no vendor.** What scrapes or receives them is a composition-root choice,
  not a decision baked into the code that records them.
- **A metric that only exists in a unit test is not observable.** The proof is a scrape and a
  trace read the way an operator would, not an assertion against an in-memory collector alone.
- **A histogram with the wrong buckets answers nothing.** Boundaries meant for milliseconds make a
  0..1 ratio meaningless, and a ratio recorded at record time cannot be re-aggregated.

## Decision Outcome

**1. A decorator over the metrics sink, feeding BCL instruments.**

`MeterAssistantMetricsSink` wraps the persisting `IAssistantMetricsSink`.
The agent records each signal once; the decorator fans it out to the instruments and then to the
sink underneath, so Prometheus and the AI dashboard read the same measurement and cannot drift.
Rejected: a second recording call inside `AssistantChatService`.
Two call sites are two things to keep in step, and the drift is silent when they fall out of step.

The instruments live on a BCL `Meter` (`AssistantInstrumentation`), not a Prometheus client type,
so the recording code names no backend.
`ObservabilityComposition` is the only place that decides who sees them: it registers the
OpenTelemetry meter provider, adds the Prometheus exporter, and — separately — an OTLP trace
exporter that turns on only when an endpoint is configured.

**2. Traces on a BCL `ActivitySource`, exported only when asked.**

`AssistantChatService` opens an `assistant.chat` span per conversation, an `assistant.model_turn`
span per Anthropic round trip (tagged with the model, token counts, and stop reason), and an
`assistant.tool` span per tool call (tagged with the tool, its kind, whether it errored, the
duration, the retrieval confidence and match count, the ticket it touched, and — on a guard
refusal — the guard and its reason, with the span status set to error).
This is the "which tools, why" record: a write is traceable to the ticket it changed, and a
refusal to the rule that refused it.

The spans always exist.
Without a listener they cost almost nothing, and enabling collection later is a configuration
change (`Observability:OtlpEndpoint`), not a code change.
Rejected: logging the decision trail as text. It reads the same span data back out of a log line
instead of querying it, and loses the parent-child structure a trace keeps.

**3. Confidence and duration get explicit histogram buckets; the confidence cut mirrors the dashboard.**

The default OpenTelemetry boundaries (0, 5, 10, … 10000) are millisecond boundaries.
A 0..1 confidence recorded against them piles into the first bucket and the histogram answers
nothing, so `ObservabilityComposition` attaches an explicit 0.1..1.0 boundary set to the
confidence histogram and an explicit millisecond set to the duration histogram via `AddView`.

The decorator records the confidence histogram only for `Retrieval` and `DuplicateCheck` kinds,
the same cut `AiDashboardAggregation.Retrieval` already makes.
A routing confidence and a grounding score share the 0..1 range but measure different things;
averaging them into one histogram would give a number no query could trust.
This is exactly the drift the single recording point is meant to prevent — the dashboard filtered
on kind and, before this was caught in a live scrape, the meter did not.

**4. The scrape endpoint sits outside the API-key guard.**

`ApiKeyMiddleware` lets `Observability:MetricsPath` (default `/metrics`) through without a key.
A scraper is infrastructure, not a client; requiring the demo API key would mean handing a Prometheus
instance the application credential.
The endpoint exposes only aggregate counters and histograms — no ticket content, no conversation
text — so it carries nothing the guard protects.

## Consequences

- `AssistantToolInvocation` gained a `Duration`, persisted (a new `DurationMs` column, migration
  `AddToolInvocationDuration`) so the AI dashboard can show average latency per tool rather than
  leaving a dead column. The EF repository maps the column back to a `TimeSpan` client-side.
- There is no error-rate instrument. An error rate is a ratio, and a ratio recorded at record time
  cannot be re-aggregated: summing two windows' rates is meaningless. The call counter carries an
  `error` label instead, and the rate is derived at query time (see the runbook for the PromQL).
- The token counter is labelled by model and direction, so spend is sliceable per model without a
  second instrument.
- Metric recording is synchronous and allocation-light, and the inner sink already swallows its own
  failures, so the decorator adds no failure mode to the chat turn.
- OpenTelemetry pulls in `OpenTelemetry.Extensions.Hosting`, the ASP.NET Core instrumentation, the
  OTLP exporter, and `OpenTelemetry.Exporter.Prometheus.AspNetCore` (only a beta is published; the
  scrape format itself is stable).
- The traces are verified against a live process, not only a collector: an evaluation-suite test
  scrapes `/metrics` and reads the spans back through an `ActivityListener`, correlating by the
  chat span's trace root so parallel test classes do not bleed into each other.

## Related

- ADR 0034: the metrics sink and `AssistantToolKind` this decorates, and the AI dashboard that
  reads the same recording point.
- ADR 0035: the sandbox whose guard refusals these spans and counters make visible.
- ADR 0023: the assistant as an edge adapter — the reason all of this lives in the API layer.
- ADR 0033: ticket summaries, whose model turns the token and turn counters also cover.
