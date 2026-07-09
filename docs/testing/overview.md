## Test Projects

| Project                         | 	Scope                                  | 	Key Patterns                                                          |
|---------------------------------|-----------------------------------------|------------------------------------------------------------------------|
| `Tests.Domain`                  | 	Domain aggregate, workflow transitions | 	`Ticket` constructor, `TicketWorkflow` guard v                        |
| `Tests.Application`             | 	Handler logic, Result monad            | 	`CreateTicketHandler`, `GetTicketByIdHandler`, `SearchTicketsHandler` |
| `Tests.Api`                     | 	HTTP binding, exception handling       | 	`ApiWebApplicationFactory` (InMemory)                                 |
| `Tests.Infrastructure.InMemory` | 	In-memory repository                   | 	`InMemoryTicketRepository`                                            |
| `Tests.EndToEnd`                | 	Full DI pipeline, both providers       | 	`[ProviderMatrix]` attribute                                          |
| `Tests.Evaluation`              | 	The agent, end to end, deterministically | 	`EvaluationHost` + `ScriptedTurn`                                   |
| `Tests.Integration`             | 	HTTP query binding                     | 	`SearchTicketsRequest` parameter parsing                              |
| `Tests.Web`                     | 	API client deserialization             | 	`TicketsApiClient` + ProblemDetails parsing                           |

---

## Agent Evaluation Suite

`Tests.Evaluation` runs the real agent against a scripted model.
The API, the SSE endpoint, the agentic loop, the guard pipeline, the tools, the command handlers and the audit trail all execute for real; the only fiction is what the model decided to say.
Both agents are covered: the chat assistant through its HTTP endpoint, and the autonomous worker (ADR 0037) through one review of one ticket, in the same autonomous scope the background loop opens.
The worker's scheduling loop is deliberately not exercised — an interval is not what those scenarios are about; what the worker may do unattended is.

The seam is the HTTP transport underneath `AnthropicClient`.
`ScriptedTurn` renders a model decision as the exact Server-Sent Events the Anthropic API would emit — text arriving one delta per word, tool arguments arriving as partial JSON fragments split mid-token.
Writing the wire rather than stubbing the SDK means the suite also covers the SDK's stream parsing and our own delta accumulation, the two places where a silent change would otherwise reach production unnoticed.
`ScriptedModelHandler` replays those turns and records every request the agent sent back, so a test can assert the `tool_result` the model actually received — the round trip no unit test sees.

**What the suite does and does not evaluate.**
The model is scripted, so this does not measure the model's judgment; no offline suite can, without calling the model.
It measures everything the agent does *around* that judgment: that the requested tools run through the real handlers in the right order, that results come back correctly shaped, that the workspace ends in the expected state, and that the client sees the expected stream.
Those are the parts that regress silently when code changes.
The model's judgment regresses visibly, and is checked against the live API by hand.

| File | Covers |
|------|--------|
| `PromptSuiteTests` | Curated prompts, each pinned to a tool sequence, an answer, and its effect on the workspace |
| `ToolCallingTests` | Execution through real handlers, rejected input as a correction, tool chaining, sandbox refusals |
| `StreamingTests` | Event order, per-word deltas, conversation continuation, and every way a stream can fail |
| `RagGroundingTests` | Citations only for passages actually retrieved; grounding scored against them |
| `MetricsRegressionTests` | The AI dashboard reflects what the run recorded |
| `ObservabilityTests` | The scrape endpoint exposes the instruments; decisions are readable as spans |
| `AutonomousWorkerTests` | The unattended worker asks, parks, grounds a proposal, and is refused everything a human should approve first |

Fixtures are deterministic and need no API key, no database, and no network.

---

## Testing Conventions

#### Project Layout Mirrors Source

```
tests/ServiceDeskLite.Tests.Application/Tickets/CreateTicket/
  ↳ tests src/ServiceDeskLite.Application/Tickets/CreateTicket/
```

#### Test Naming

`<MethodOrScenario>_<Condition>_<ExpectedOutcome>`

Examples:

- `NewTicket_StartsWithStatus_New`
- `ChangeStatus_InvalidTransition_ThrowsDomainException_WithExpectedErrorCode`
- `HandleAsync_NullCommand_ReturnsValidationFailure`
-
#### Assertion Library

All tests use FluentAssertions. Prefer `.Should().Be(...)` over `Assert.Equal.`
