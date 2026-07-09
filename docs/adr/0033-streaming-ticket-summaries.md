# ADR 0033: Streaming Ticket Summaries (marker-delimited sections over SSE)

## Status

Accepted

## Context and Problem Statement

Issue #160 asks for a live-streamed AI summary of a single ticket — summary, next steps,
risks, and missing information — rendered on the ticket details page so an agent can triage
without reading the whole history.

The two acceptance criteria pull against each other.
A **structured** result wants a schema.
**Token-by-token streaming** wants a flat text stream.
Anthropic's structured outputs (`output_config.format`) satisfy the first and technically
stream, but only as partial JSON: a client cannot render `{"summary":"The user rep` as
anything meaningful, so the visible result is a long pause followed by a sudden full answer.

Decisions:

1. **How is the output structured** without giving up progressive rendering?
2. **Is this a tool** in the assistant's agentic loop, or a separate endpoint?
3. **Where does the section boundary get recovered**, given that markers do not arrive
   whole in the token stream?

## Decision Drivers

- **Progressive rendering is the feature**, not a nicety: the acceptance criterion says
  token-by-token, and a summary that appears all at once after five seconds reads as a
  slow page, not as a live one.
- **Reuse the existing SSE path** (ADR 0023): the API already re-streams Anthropic events
  as Server-Sent Events, and the web client already consumes them with `SseParser`.
- **Grounding over fluency**: an invented assignee or deadline is worse than an empty
  section. The prompt must have somewhere honest to put what it does not know.
- **The model must not act.** ADR 0023's invariant — the model reaches the domain only
  through application-layer command handlers — is about tools that change state.

## Decision Outcome

**1. Marker-delimited sections, parsed out of the token stream.**

The model writes plain prose, each section introduced by a marker on its own line
(`<<SUMMARY>>`, `<<NEXT_STEPS>>`, `<<RISKS>>`, `<<MISSING_INFO>>`).
`SummarySectionParser` consumes the text deltas, recovers the section boundaries, and the
service re-emits each fragment as an SSE `delta` event tagged with its section.
The client appends each delta to the matching panel, which fills as the text arrives.

Rejected: `output_config.format` with a JSON schema. It guarantees the shape, but the client
would need a partial-JSON parser and the panels would still jump at field boundaries. The
guarantee we actually need — four sections, in order — is cheap to recover from markers, and
a malformed stream degrades to visible text rather than to a parse failure.

**2. A separate read endpoint, not a tool.**

`GET /api/v1/tickets/{id}/summary` streams the summary. It is not registered in the
assistant's tool list.
The summary informs an agent; it never changes a ticket. Because nothing is acted on, the
"model can only act through command handlers" invariant does not apply here — there is no
action to guard. Making it a tool would enlarge the chat loop's tool surface for a pure
read, and would put a UI concern inside the agentic loop.

The ticket is resolved through the existing `GetTicketByIdHandler` **before** the stream is
opened, so an unknown ticket returns a 404 ProblemDetails rather than a 200 stream whose
first event is an error.

**3. The parser buffers across delta boundaries.**

A marker is not atomic in the token stream: `<<NEXT_STEPS>>` can arrive as `<<NEXT_` followed
by `STEPS>>`. The parser therefore holds back any tail that is still a viable marker prefix
and releases it as literal text only once it can no longer become one. This is the one place
where the feature can break silently — a naive per-chunk match either leaks `<<NEXT_` into
the UI or loses the section switch — so the parser is a pure class with no dependency on the
HTTP stack, and it is covered by unit tests down to character-by-character streaming.

**4. Grounding is a prompt obligation with an escape hatch.**

The prompt feeds the ticket's description, status, priority, assignee, due date, allowed
transitions, comments, and audit history, and instructs the model to route anything it does
not know into the `missing information` section instead of assuming it. That section carries
the hallucination load: it gives unknowns a legitimate home, rather than leaving the model to
smuggle them into the summary as fact.

Thinking is left off and the output budget is capped (`Anthropic:SummaryMaxTokens`, 2048).
The panel is a triage aid; a long generation is a failure mode, not a feature.

## Consequences

- The summary costs one model call. It is streamed on first open of the `AI Summary` tab and
  cached for the lifetime of the page — switching tabs does not regenerate it. Navigating to
  another ticket cancels any in-flight stream.
- Section content is rendered as text, never as HTML. Markdown list markers are interpreted
  only far enough to emit real `<li>` elements; model output is never treated as markup.
- If the model omits a marker, that section stays empty and the others still render. If it
  emits an unknown marker, the text surfaces verbatim instead of vanishing.
- The summary is not persisted. It reflects the ticket at read time; caching it would mean
  invalidating it on every comment, status change, and assignment.
- The endpoint has no rate limiting (out of scope repo-wide), so a client that reloads the
  tab repeatedly pays for a model call each time. The page-lifetime cache is the only guard.

## Related

- ADR 0023: AI assistant at the edge, SSE streaming, tools bounded by application handlers.
- ADR 0026: server-side conversation state — deliberately not reused; a summary is stateless.
