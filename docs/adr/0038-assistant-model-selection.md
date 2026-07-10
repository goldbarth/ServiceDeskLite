# ADR 0038: Assistant Model Selection (Sonnet 5, thinking disabled)

## Status

Accepted.

## Context and Problem Statement

The assistant has run on `claude-opus-4-8` since it existed.
That was never a decision.
It was the default the first request was written with, and nothing since then forced anyone to justify it.

What the assistant actually does is orchestration.
It picks among twelve tools, runs at most six model-tool round trips per request (ADR 0023), streams tokens over SSE, summarizes tickets into four short sections (ADR 0033), and answers from a knowledge base with cited passages and a grounding self-check (ADR 0029, ADR 0031).
None of that is deep reasoning.
It is choosing the right tool, filling its arguments correctly, and reading the result.

Two things made the default worth revisiting.

The first is volume.
Since ADR 0037 an autonomous worker scans open tickets on an interval and reviews them without a person present.
The interactive assistant costs what someone types; the worker costs what the clock says.
Opus-tier pricing sits at $5 per million input tokens and $25 per million output tokens.

The second is latency.
Every interactive turn is a Server-Sent Event stream, so the time to the first text delta is not a metric in a dashboard - it is a cursor blinking at a person who is waiting.

There is also a trap in the migration itself.
Sonnet 5 enables adaptive thinking when the `thinking` field is absent from the request.
Opus 4.8 runs without thinking under the same conditions.
The field is absent in both call sites.
Changing only the model string would therefore have changed a second thing, silently, and thinking tokens count against `MaxTokens`.

## Decision Drivers

- **A default nobody chose is not a decision.** Whatever model runs, the reason it runs has to be writable down.
- **The expensive agent is the one nobody watches.** The worker sets the cost floor, not the conversation.
- **Latency is visible here.** SSE puts the model's first token in front of a person.
- **Errors are not equally cheap.** A wrong tool argument gets refused by a guard and retried. A wrong grounding verdict cites a passage that does not support the answer, and nobody notices.
- **Silent behavior changes are the ones that survive review.** A missing request field that means different things on different models must be made explicit, not left to the default.

## Decision Outcome

**1. The assistant runs on `claude-sonnet-5`.**

Sonnet 5 reaches near-Opus quality on agentic and tool-calling work, which is the entire workload, at $3 per million input tokens and $15 per million output tokens - roughly 40% of the Opus bill, and less than that until the introductory pricing ends on 2026-08-31.
It keeps the same 1M context window, so nothing about the persisted conversation state (ADR 0026) or the retrieved passages in a RAG turn has to be resized.
It is faster, which the stream shows.

The model stays in `AnthropicOptions.Model`, bound from configuration.
A deployment that wants Opus back changes a setting, not a class.

**2. Thinking is disabled explicitly, at both call sites.**

`AgentLoop` and `TicketSummaryService` each set `Thinking = new ThinkingConfigDisabled()`.

The reason is the token budget, not determinism.
The model is not deterministic with thinking off either - Sonnet 5 does not accept `temperature` at all, and `temperature: 0` never guaranteed identical output on the models that did.
What makes the tool chain predictable is the iteration ceiling and the guard pipeline (ADR 0035), and neither of those changes with thinking.

What does change is the budget.
`MaxTokens` is 8192 for a chain that may run six round trips, and `SummaryMaxTokens` is 2048 for four deliberately short sections.
Thinking tokens are drawn from the same ceiling.
A long chain would have found the ceiling with reasoning it never showed anyone.

The second reason is the stream.
Adaptive thinking can spend hundreds of tokens before the first text delta arrives, and with the default `display: "omitted"` those blocks carry no text.
The user would see a pause and nothing else.

Rejected: **thinking on, with a raised `MaxTokens`.**
It buys the budget back and leaves the pause.

Rejected: **thinking on, with `display: "summarized"`.**
The reasoning becomes visible, which is a product decision about what the assistant shows a person, not a way to fix a budget.
Nothing in the current UI has a place to put it.

Rejected: **thinking on for the worker, off for the chat.**
Superficially attractive - the worker has no stream and nobody waiting - but the two agents share one `AgentLoop`, one per-owner model-turn bucket, and one set of sandbox budgets (ADR 0035).
Two thinking policies would mean two token profiles drawing on one budget, and two behaviors to reproduce when a tool chain misbehaves.
The loop is shared for the same reason the guardrail is: a rule applied in one place cannot be forgotten in the other.

**3. Haiku 4.5 is not used.**

It is cheaper again, and its 200K context window would eventually collide with a long conversation carrying retrieved passages and long-term memory.
The stronger objection is where the model's judgment actually matters.
`check_grounding` decides whether an answer is supported by its sources, and the worker decides which of its intentions are consequential enough to hand to a person.
Those are the two places where a weaker model's mistake does not surface as a refused tool call but as a plausible wrong answer.
The savings are real; they are not worth buying there.

## Consequences

- **The model bill drops by roughly 60% at list price** for identical traffic. `IAssistantMetricsSink` already tags token usage with the model (ADR 0034, ADR 0036), so the dashboard shows the old and the new cost side by side rather than as one unexplained step.
- **No token re-baselining is needed.** Sonnet 5 uses the same tokenizer as Opus 4.8, introduced with Opus 4.7. The same prompt counts the same number of tokens, so `MaxTokens` and `SummaryMaxTokens` keep the meaning they were tuned with. This is worth verifying with `count_tokens` on a real summary prompt rather than assumed.
- **Sonnet 5 reaches for tools less readily when thinking is disabled.** This is the cost of the decision above, and it lands squarely on an assistant whose entire purpose is calling tools. The countermeasure is prompt-side and cheap: each tool's description states *when* it should be called, not only what it does, and the system prompt says so as well. `ServiceDeskLite.Tests.Evaluation` is the instrument that shows whether the tool-call rate holds - it proves the harness is correct, not that the model is good, and a drop in tool selection is exactly the kind of regression it can see.
- **Sonnet 5 follows instructions more literally than Opus 4.8.** Prompt lines written to push a more reluctant model - "always", "if in doubt" - now apply at face value. The twelve `*.prompt.cs` files are the place to look when a tool starts firing more often than it should.
- **A missing `Thinking` field is now a defect, not an omission.** Nothing in the API rejects a request without it; the model simply behaves differently. A test asserts that both `MessageCreateParams` set it, because the next call site added to the assistant will otherwise inherit adaptive thinking without anyone deciding so.
- **The model is still an implementation detail of the edge adapter.** Domain and Application compile without an Anthropic reference (ADR 0023), and swapping `claude-sonnet-5` for anything else touches one configuration value and no layer boundary. This ADR records a trade-off, not a coupling.

## Related

- ADR 0023: the assistant as an edge adapter. The model lives outside the architecture, which is what makes this decision reversible by configuration.
- ADR 0026: server-side conversation state. Unchanged - Sonnet 5 keeps the 1M context window the transcript and long-term memory were sized against.
- ADR 0029, ADR 0031: knowledge-base retrieval and the grounding self-check. The two places where model quality, not model speed, is the thing being bought.
- ADR 0033: streaming ticket summaries. `SummaryMaxTokens` is the tightest budget in the system and the reason thinking is disabled there too.
- ADR 0034, ADR 0036: the metrics that make a model change legible after the fact, through the `model` tag on token usage.
- ADR 0035: the sandbox. Its budgets and its refusal-as-tool-result mechanism are what make the tool chain predictable - not the model, and not thinking.
- ADR 0037: the autonomous worker. It sets the token volume that makes the price difference matter, and it shares the loop that makes a single thinking policy the right shape.
