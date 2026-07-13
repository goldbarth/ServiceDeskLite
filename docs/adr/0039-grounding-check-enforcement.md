# ADR 0039: Grounding-Check Enforcement (tool_choice, not a prompt request)

## Status

Accepted.

## Context and Problem Statement

ADR 0031 gave the assistant a grounding self-check.
After it answers from knowledge-base passages, `check_grounding` scores the draft against the passages actually retrieved and returns the unsupported sentences so the model can hedge or re-retrieve before it asserts.

Nothing enforced the call.
The system prompt asked for it:

> Before you send an answer that relies on those knowledge-base passages, verify it with the check_grounding tool.

`AgentLoop` did not require it, no guard demanded it, and `tool_choice` was never constrained.
If the model skipped the line, the answer streamed out unverified and looked exactly like a verified one.

This was the only safety property in the assistant phrased as a request.
Every other bound is a mechanism: the guard pipeline refuses tool calls (ADR 0035), the command handlers validate (ADR 0023), the state machine rejects illegal transitions, and `HumanReviewGuard` stops the worker before a consequential write (ADR 0037).
ADR 0037 already states the standard this failed: *"A rule the model can talk itself out of is not a rule."*

Two things make the gap worse than a missing nicety.

Whether the model calls the tool is model behaviour, and model behaviour changes with every release (ADR 0038, issue #186).
Tying a correctness property to it means re-validating on every model change, forever.

And the failure is silent.
Every other bound fails loudly: a refused tool call returns an `is_error` result the model reads and a metric an operator sees.
A grounding check that never runs produces nothing at all.

## Decision Drivers

- **A safety property has to be a mechanism.** If the model can decline it, it is a suggestion, and the model's willingness to follow suggestions moves with the model.
- **Streaming must stay intact.** The app streams tokens live (ADR 0023). An answer must not be buffered until verified, nor streamed twice.
- **The verdict must arrive before the first visible token.** A check that can only warn after the answer is on screen is the wrong output: the user has not read the passages and cannot act on a score.
- **One loop, both callers.** The interactive assistant and the autonomous worker share `AgentLoop` (ADR 0037). A bound added for one must hold for the other.
- **The detector is currently weak (issue #188).** `GroundingEvaluator` is lexical, so a correct paraphrase or a cross-lingual answer scores low. Whatever is enforced must be defensible while that is still true.

## Decision Outcome

**The loop forces `check_grounding` through `tool_choice` once passages have been retrieved and no check has run.**

At the top of each iteration `AgentLoop` computes:

```csharp
var forceGrounding = !groundingChecked && _retrieval.Passages.Count > 0;
```

When it holds, the request narrows `tool_choice` to the single tool `check_grounding`; otherwise the field carries `auto`, as before.
On the forced turn the model cannot stream a text answer.
It writes its draft into the tool's `answer` argument, the check runs against `IRagRetrievalContext`, and the verdict returns as a `tool_result` the model reads before it composes the answer the user sees.
`groundingChecked` flips as soon as a `check_grounding` call appears in a turn, whether the model was forced into it or called it on its own.

The system prompt still asks for the check.
A model that self-checks satisfies the condition and is never forced, which saves the extra model turn.
The enforcement is the backstop for the turn the model would otherwise have skipped.

**Force-once, not force-until-covered.**

The constraint fires exactly once per run: the first turn after passages appear.
After that check the model is free again, even if it retrieves more passages later.
This guarantees at least one grounding check after any retrieval, which is the property that was missing.
It does not guarantee that every passage retrieved across a long chain was grounded before the final answer.
The stricter invariant - re-force whenever new, un-grounded passages appear - needs passage-delta tracking and is deferred until there is evidence a run needs it.

**Rejected: grade in the loop after the answer completes.**
Deterministic and free, using `GroundingEvaluator` directly.
But the text has already streamed, so it can only warn, and a warning the user cannot act on, attached to an answer already on their screen, is the wrong output.

**Rejected: buffer the answer until graded.**
Withholds an ungrounded answer, at the cost of the live streaming ADR 0023 deliberately built.

## Consequences

- **The grounding check is now a mechanism.** An answer that relies on retrieved passages cannot reach the user without at least one grounding verdict having been computed first. That property no longer depends on the model choosing to honour a prompt line.
- **It holds for the worker too.** The autonomous reviewer runs the same loop, so an autonomous answer from the knowledge base is subject to the same forced check, with nobody watching.
- **It costs one model turn when the model did not self-check.** The prompt still steers the model to check on its own, and a self-check avoids the extra round trip. The forced turn only appears when the model would otherwise have answered unverified.
- **The verdict is not surfaced to the user or a dashboard, and that is deliberate (issue #188).** `GroundingEvaluator` is lexical: a correct paraphrase, a correct translation, and the exact hedging the prompt asks for all score low. Forcing the check feeds those false alarms to the *model*, which can recognise its own paraphrase and answer anyway - an improvement over skipping the check. Showing the same score to a person would turn the false alarms into noise they learn to ignore. Surfacing waits on a semantic detector (issue #188).
- **The enforcement is testable without a model.** `ServiceDeskLite.Tests.Evaluation` reads the `tool_choice` the loop sends upstream, so the constraint is asserted deterministically: forced after retrieval, not forced when the model self-checks, never forced without a retrieval, and forced only once.

## Related

- ADR 0023: the assistant as an edge adapter, and the streaming value the buffering option would have given up.
- ADR 0031: the grounding self-check this enforces. That ADR built the tool and left the call to the prompt; this one makes the call a mechanism.
- ADR 0035: the sandbox. Its refusal-as-tool-result mechanism is the model this follows - a bound the model reads and adjusts to, not an exception.
- ADR 0037: the autonomous worker, the shared loop, and the standard "a rule the model can talk itself out of is not a rule."
- ADR 0038, issue #186: model behaviour, including tool-call readiness, changes per release, which is why a correctness property cannot ride on it.
- Issue #188: the lexical detector that gates surfacing the verdict anywhere a person or a threshold would read it.
