# ADR 0035: Agent Sandbox (uniform tool admission, rate limits, write budgets)

## Status

Accepted. Reverses the "rate limiting is out of scope" position of ADR 0023.

## Context and Problem Statement

Issue #162 asks for hardened tool execution: every tool behind a uniform guard layer, rate
limiting, and failures that propagate as structured results rather than exceptions.

One of the three already held. `ToolRetryPolicy` catches every non-transient exception a tool
throws and turns it into an `is_error` tool result, so the model always receives a response and
the stream never breaks; only a client disconnect propagates. That is criterion three, and it
needed evidence, not code.

The other two did not. Each tool validated its own arguments, and nothing constrained a tool
call before it reached a command handler. Nothing bounded how many writes one chat turn could
perform, and nothing bounded model spend at all: `Anthropic:MaxToolIterations` limits round
trips within a request, but a client may issue requests without limit, and the ticket-summary
endpoint (ADR 0033) called the model on every first open of a tab, deliberately unlimited.

Decisions:

1. **Where does admission happen**, so a tool cannot be added past it?
2. **What is rate limited, at what granularity, and where does the state live?**
3. **What does "safety check" mean** for a system whose tools already validate their inputs and
   whose handlers already enforce the domain rules?

## Decision Drivers

- **A guard that can be bypassed is not a guard.** It must sit where the model's intent becomes
  execution, not in a base class a new tool may simply not inherit from.
- **A refusal is not a failure.** The model corrects itself when a tool returns `is_error` with a
  reason. A refused call should read to the model exactly like a validation error.
- **Bounds are safety nets, not business rules.** A well-behaved conversation never reaches one.
  The domain rules stay in the handlers, where they already are.
- **Do not put a database in front of the thing you are protecting.** A limiter that fails when
  the store fails has made the system less available, not more.

## Decision Outcome

**1. A guard pipeline at the single admission point.**

`ToolGuardPipeline` runs every `IToolGuard` in `AssistantChatService`, immediately before
`ToolRetryPolicy` executes the call. A refusal short-circuits execution and becomes an ordinary
`is_error` tool result carrying the guard's reason. The model reads it and adapts — in practice
it explains to the user what it did not do, and offers to continue.

`IToolGuard` splits `Check` (pure) from `Commit` (spends). The pipeline checks every guard, and
only once all of them admit the call does it commit any of them. Without that split, whichever
guard ran first would spend its allowance on a call a later guard then refuses: a rate-limit
token burned on a write the write budget rejects. The split also makes the guards
order-independent, so registration order carries no meaning.

Rejected: a base class each tool inherits. It cannot be enforced — a new tool that does not
inherit it is simply unguarded, and nothing fails.

**2. Rate limits are per-owner token buckets, in process memory.**

Two buckets per owner, both refilling continuously rather than per fixed window (so a caller
cannot spend one window's allowance at its end and the next one's at its start):

- `tool-calls`, spent by `RateLimitGuard` on each admitted tool call;
- `model-turns`, spent by `ModelTurnLimiter` on each Anthropic round trip.

The model-turn bucket is shared by the chat loop and the ticket-summary endpoint, because to the
account they are the same spend. This closes the gap ADR 0033 left open.

`ICurrentUser.Owner` is the key. It is a constant demo owner today, so the limit is effectively
global; when real authentication lands at that seam, the limit becomes per user with no change
here.

State is a `ConcurrentDictionary` of buckets in the API process. Rejected: persisting them like
the assistant metrics. A per-minute limit is meaningless across a restart, a database round trip
before every tool call makes the limiter a dependency of what it protects, and a limiter that
must decide whether to block or pass when its store is down is a separate decision nobody asked
for. The cost is honest and stated: the limit is per instance, and a multi-instance deployment
would need a shared store.

`RateLimitGuard.Check` peeks and `Commit` takes the token, per the contract. Two turns racing
between the two can each see the last token and both proceed. The overshoot is bounded by the
number of concurrent turns for one owner and disappears as the bucket refills; closing it would
mean holding a lock across the whole guard chain, which is a worse trade for a safety net.

**3. "Safety check" is three concrete rules.**

- **Input caps** (`InputSizeGuard`): a limit on the raw argument JSON and on any single string
  inside it, checked before a tool parses anything. Tool input is model output shaped by ticket
  text, and ticket text is user-supplied; this is the first place that content is measured rather
  than trusted. The caps sit above the domain's own limits, so a value that passes here can still
  be rejected by the handler that owns the rule.
- **A write budget per turn** (`WriteBudgetGuard`): at most N ticket- or memory-changing calls in
  one chat turn. `MaxToolIterations` bounds round trips, but a model may request several tools per
  round, so it does not bound writes. A runaway retrieval loop wastes tokens; a runaway write loop
  changes the workspace. Retrievals are never counted, so a spent write budget does not silence
  the model's reads.
- **Refusing unknown tool names** (`KnownToolGuard`): a name absent from `ToolCatalog` has no
  declared kind, therefore no write classification, and would slip past the write budget.

`ToolCatalog` is the one list of tools and their kinds. The sandbox reads it to admit a name, the
AI dashboard (ADR 0034) reads it to aggregate, and the dispatch reads the names. A test asserts
every tool type in the assembly appears in it.

Rejected: validating arguments centrally against each tool's JSON schema. It would remove some
duplication, but it moves the error message away from the tool that can phrase it best for the
model, and per-tool `TryParseInput` is already unit-tested.

## Consequences

- Every tool call passes four checks before reaching a handler. All are in-memory comparisons;
  none add I/O.
- A refused call is recorded by the metrics sink like any other error result, so refusals show up
  in the AI dashboard's per-tool error rate rather than vanishing.
- Exhausting the model-turn budget ends the chat turn with an SSE `error` event, and the summary
  endpoint with the same. Neither returns HTTP 429: the response has already begun streaming by
  the time the model is called, and the existing clients read `error` events. An HTTP-level
  limiter in front of the endpoints remains a separate concern.
- The limits are per API instance and reset on restart. Stated in the runbook.
- Defaults are deliberately loose (60 tool calls and 30 model turns per minute, 6 writes per
  turn). They bound a runaway agent, not a busy human.
- ADR 0023 listed rate limiting as out of scope, and the repository's guidance required an ADR to
  reverse that. This is it; the guidance is updated accordingly.

## Related

- ADR 0023: the assistant as an edge adapter, tools bounded by application handlers. Its "no rate
  limiting" consequence is superseded here.
- ADR 0027: `ToolRetryPolicy` and transient-fault classification — the reason structured error
  propagation was already satisfied.
- ADR 0033: ticket summaries, whose unlimited model calls this ADR bounds.
- ADR 0034: `AssistantToolKind` and the metrics sink, which `ToolCatalog` now feeds.
