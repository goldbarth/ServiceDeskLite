# ADR 0037: Autonomous Ticket Worker (one agent loop, two agents, one guardrail)

## Status

Accepted.

## Context and Problem Statement

Issue #165 asks for a background worker that resolves tickets end to end within defined policies:
scan open tickets, detect missing information, ask follow-up questions, propose solutions, close and
open tickets — and do all of it inside a human-review guardrail.

Everything the worker needs to *act* already exists.
The tools reach the domain only through the application-layer command handlers (ADR 0023), the
sandbox bounds what a tool call may do (ADR 0035), retrieval is grounded and self-checked
(ADR 0029, ADR 0031), and every action it takes is already audited and measured (ADR 0034,
ADR 0036).

What did not exist is an agent that runs without a person.
Three things stood in the way, and each of them is a decision:

1. **The tool-calling loop was the chat endpoint.**
   `AssistantChatService` interleaved the agentic loop with `yield return` of SSE items. A worker has
   no client and no stream. Either the loop is extracted, or there are two loops.
2. **Every write tool named `ai-assistant` as its audit actor**, hard-coded. A background process
   writing under the assistant's name makes the audit trail lie about who decided.
3. **Nothing distinguished a high-impact action from a routine one**, because in a conversation the
   distinction does not exist: the user asked, and the user is watching.

## Decision Drivers

- **A rule the model can talk itself out of is not a rule.** What the worker may do unattended has to
  be enforced where execution happens, not requested in a prompt.
- **A refusal is not a failure.** ADR 0035 established that a guard refusal returns to the model as an
  ordinary `is_error` tool result, and the model adapts. That mechanism is exactly what a review
  guardrail needs.
- **Two loops will drift.** The same drift the metrics decorator was introduced to prevent (ADR 0036)
  would reappear, one layer down: guards, retries, budgets and spans applied to the chat and
  forgotten in the worker.
- **An unattended agent is a stranger to the person who owns the ticket.** Whatever it did must be
  legible afterwards, and whatever it wanted to do must have reached a human before it happened.

## Decision Outcome

**1. The loop is extracted; the chat becomes an adapter.**

`AgentLoop` owns the model turns, the tool dispatch, the guard pipeline, the retries, the metrics and
the decision spans.
It emits `AgentLoopEvent`s — text, tool call, tool result, completed, error.
`AssistantChatService` translates those into SSE and keeps everything that is true only of a
conversation: the persisted transcript (ADR 0026) and the date and timezone the model resolves
"by Friday" against.
`TicketReviewer` consumes the same events and logs them.

Rejected: a second, simpler loop for the worker.
It is the smaller diff and the larger mistake — every bound the sandbox places on the agent would
have to be remembered twice, and nothing would fail when it was not.

The loop never throws across its boundary: an upstream fault, an exhausted budget, or a response
without a stop reason all leave as an `AgentErrorEvent`.

**2. The actor and the mode come from the scope, not from the tool.**

`IAgentActor` answers two questions: which actor a write is audited under, and whether a human is
watching.
Every request scope is `Interactive`/`ai-assistant` by construction.
The worker opens one scope per ticket and switches it to `Autonomous`/`ai-worker`, which is the only
place in the process where the autonomous actor comes into existence.

`AuditActors.AiWorker` is a new, distinct actor. Reading the trail, "the assistant did this while I
was talking to it" and "a background process decided this without me" are not the same event, and
only the second one needs explaining. The automation rate now counts both, through
`AuditActors.Automated` — one list, so the two persistence providers cannot come to disagree about
what "automated" means.

A scope per ticket is also what keeps one ticket's retrieved passages from grounding another
ticket's answer: `IRagRetrievalContext` is scoped.

**3. The guardrail is a guard, and the refusal is the instruction.**

`HumanReviewGuard` joins the sandbox pipeline. It constrains `Autonomous` runs only:

- reads are always allowed — reading a ticket harms nobody;
- writes listed in `AutonomousWorker:AutonomousWrites` are allowed, which by default is
  `add_comment` alone;
- `change_ticket_status` is allowed for the transitions in
  `AutonomousWorker:AutonomousStatusTransitions`, by default `Triaged` and `Waiting`;
- everything else is refused, and the refusal tells the model to post its proposal as a comment
  instead, and not to retry.

That last sentence is the whole guardrail. The model reads the refusal, comments, and a person
decides — the reasoning reaches a human and the ticket is untouched until that human agrees.
Closing a ticket, opening a follow-up ticket, assigning, updating and routing all land there.

`Triaged` is in the autonomous set for a reason the workflow forces: the only transition out of `New`
is to `Triaged`, so without it the worker could ask a question on a new ticket but never park it, and
the ticket would sit in the queue looking actionable while it waited for a reply. Triaging classifies;
it finishes nothing. `Resolved` and `Closed` are absent because finishing someone's ticket is a
decision a person makes.

Rejected: **a persisted approval queue** with its own entity, endpoints and page. It is the cleaner
separation and a feature in its own right; a comment on the ticket puts the proposal where the person
who owns it is already looking, and needs no schema.
Rejected: **a shadow mode that never writes.** It is the safest thing that does not meet the
requirement — an agent that cannot ask a question is an observer.

**4. The worker owns *when*; the reviewer owns *what*.**

`TicketWorker` is a `BackgroundService`: interval, candidate selection, and the promise that one bad
ticket does not end the loop and one bad scan does not end the worker.
`TicketReviewer` reviews one ticket and is directly testable — it refuses to run in an interactive
scope rather than quietly writing under the wrong actor.

Candidates are the oldest tickets first, so none starves behind newer arrivals, and only tickets past
`MinTicketAgeMinutes`, because a ticket created seconds ago may still be having its details typed in.
`Waiting` is not scanned: the worker moves a ticket there when it needs a human, and scanning it
again would mean asking the same question twice.

The reviewer's prompt carries the ticket's comments so far and its legal status transitions. Without
the comments it would ask the same question on every scan; without the transitions it would guess at
a state machine that is authoritative and would read its own rejection back.

## Consequences

- The worker is **off unless `AutonomousWorker:Enabled` is set**. A service that starts writing to
  tickets the moment it boots is not something anybody should get by default, least of all in a demo
  someone cloned.
- A twelfth tool, `add_comment`, exists for both agents. It is the worker's only unconditional way to
  reach a person, and the assistant gains the ability to record a comment as well.
- The worker spends the same per-owner sandbox budgets as the chat (ADR 0035). Today the owner is the
  single demo identity, so a busy worker and a busy user share one bucket. Real authentication at the
  `ICurrentUser` seam separates them without changing anything here.
- Worker runs appear in the existing traces and metrics: `worker.scan` and `worker.ticket` spans, and
  every tool call and token already counted by ADR 0036. The `agent.mode` tag separates the two
  agents in a query.
- A guard refusal is recorded like any other error result, so referrals to a human show up in the AI
  dashboard's per-tool error rate. That is honest — nothing was written — but it does mean a healthy
  worker raises the error rate of the tools it is not allowed to call.
- The evaluation suite (#163) grew worker scenarios. As before, it proves the harness is correct, not
  that the model is good.
- The policy is configuration, not prompt. Widening what the worker may do is a deployment decision
  with a validated options binding behind it, and it does not require touching the guard.

## Related

- ADR 0023: the assistant as an edge adapter; the model decides *what*, the handlers decide *whether*.
  The worker changes nothing about that invariant.
- ADR 0026: server-side conversation state, which the worker has no use for and does not create.
- ADR 0032: routing applied autonomously when confident — the precedent for `Triaged` needing no
  human.
- ADR 0035: the sandbox, whose `Check`/`Commit` split and refusal-as-tool-result mechanism this
  guardrail is built from.
- ADR 0036: the observability the worker inherits, and the `IAssistantMetricsSink` that now sees two
  agents.
