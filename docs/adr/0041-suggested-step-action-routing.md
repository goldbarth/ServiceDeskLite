# ADR 0041: Suggested Next Steps Carry Action Routing (server-tagged, client-executed)

## Status

Accepted

## Context and Problem Statement

Issue #211 asks that the most likely next step for a ticket become a primary button, not
the grey caption it is today. `TicketResponse.SuggestedNextSteps` already crosses the wire,
but as a `IReadOnlyList<string>` of prose: "Assign an owner so responsibility ... is
explicit.", "Prepare the next workflow move: Triaged.", and so on.

To turn a step into a button, the client must know which command handler the step routes to
(status change, assign, comment) and, for a status change, the target status. The suggestions
are generated in the Application layer, where the *reason* for each is known - the "assign"
suggestion exists precisely because `AssignedAgentId is null`. The question is where the
prose-to-action mapping lives.

## Decision Drivers

- **One source of truth.** The layer that decides a step should be suggested also knows what
  it means. Re-deriving that meaning elsewhere duplicates the priority logic.
- **No prose parsing at the edge.** Matching button behaviour on the wording of a sentence is
  brittle: a copy edit on the server would silently turn a live button into a dead one.
- **The client still only acts through existing handlers** (ADR 0023 invariant for the
  assistant, and the same discipline for the web): a suggestion routes to a handler, it is
  not a new automation.

## Decision Outcome

**Tag each suggestion with its action at generation time.**

`SuggestedNextSteps` becomes `IReadOnlyList<SuggestedStep>` where a step is
`(string Text, SuggestedActionKind Action, TicketStatus? TargetStatus)`.
`SuggestedActionKind` is `None | ChangeStatus | Assign | Comment`. The Application layer owns
a parallel `SuggestedStepDto`; the API maps it to the contract with the existing
`ToContract()` enum mappers. `None` marks advisory steps (e.g. "add a due date", which is a
details edit, not one of the routed actions) so the client renders them as text.

**Actionable steps are ordered first.** The generator emits Assign and the workflow move
before advisory steps, so the client's rule "the first step is the primary button" yields a
live action whenever one exists, and falls back to a caption only when nothing is actionable
(e.g. a Closed ticket).

**The client renders, never interprets.** The web makes the first step the primary control
(a button for `ChangeStatus`/`Comment`, an agent picker for `Assign`), puts the rest behind
an overflow menu, and shows `None` steps as non-clickable text. Every action calls the same
`ITicketsApiClient` handler the manual controls use.

Rejected: keeping the wire contract as strings and re-deriving the action in the web from the
ticket's state. It needs no backend change, but it duplicates the suggestion-priority logic
across server and client, and the two can drift - the exact brittleness this ADR avoids.

## Consequences

- The contract shape changed; the OpenAPI snapshot and generated docs regenerate accordingly.
- Adding a new suggestion means choosing its `SuggestedActionKind` at the point it is added -
  the mapping cannot be forgotten because it is a constructor argument.
- Assign and Comment reach their handlers through UI that already exists (the assign popover,
  the comments tab); only `ChangeStatus` executes in one click, because only it has a
  fully-determined target.
