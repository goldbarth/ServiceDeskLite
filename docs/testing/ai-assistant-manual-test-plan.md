# AI Assistant - Manual Test Plan

A manual pass over every assistant feature, with the root cause of each finding traced back into the code.
Written to be re-run: the prompts are literal, the expectations are checkable, and every deviation names the file and line it came from.

Status legend: empty = not run, ✅ passed, ✖️ failed, ⚠️ partial / notable, ⏭️ skipped (prerequisite missing or invalidated)

Version under test: `6f4297a` (Sonnet 5, thinking disabled - see ADR 0038).
Causes were traced into the code after the manual pass; findings are collected under [Root-cause analysis](#root-cause-analysis).

---

## 0. Setup (prerequisites)

| Status | Feature group | Runs on | Requires | Comment (result / recommendation) |
|---|---|---|---|---|
| ✅ | Chat + ticket tools + summaries + routing + AI dashboard | InMemory (`dotnet run`) | Anthropic key | |
| ✖️ | RAG (ticket similarity, knowledge base, grounding, memory) | Postgres + pgvector (`docker compose up --build`) | Anthropic key + `Voyage:ApiKey` | **The stack never started** ([B8](#b8), #197). Every row below marked "Postgres + Voyage" in fact ran against InMemory on `:5300`. After the port fix, knowledge-base RAG was verified to work (#192). The remaining rows must be re-run. |
| ✅ | Autonomous worker | either | `AutonomousWorker__Enabled=true` | |

Set the secrets:

```bash
dotnet user-secrets set Anthropic:ApiKey sk-ant-... --project src/ServiceDeskLite.Api
dotnet user-secrets set Voyage:ApiKey    pa-...     --project src/ServiceDeskLite.Api
```

If a host port is already taken, override it (see the runbook):

```bash
DB_PORT=5442 API_PORT=8090 docker compose up --build
```

---

## 1. Assistant chat (`POST /api/v1/assistant/chat`, web: Assistant page)

### 1.1 Actions (writes, routed through the same command handlers as REST)

| Status | Tool | Test prompt | Prerequisite | Comment (result / recommendation) |
|---|---|---|---|---|
| ✅ | `create_ticket` | "The printer on the 3rd floor stopped responding, several colleagues are affected." | InMemory | |
| ⚠️ | `update_ticket` | "Set the priority to Critical" (after the create) | InMemory | The assistant confirmed the change but never made it. Asked why, it answered: "I did **not actually check** this, I merely claimed the change without really executing the `update_ticket` call." It then looked the ticket up and asked "Shall I now actually downgrade it to High?" instead of acting. Only after a repeated confirmation did the change happen.<br><br>**Cause:** [B1](#b1) (claimed execution) plus [B6](#b6) (needless confirmation). The agent loop is correct; the behaviour comes from the model. |
| ✅ | `change_ticket_status` | "Set the login ticket to InProgress" | InMemory | Only legal transitions are applied (e.g. New -> Triaged). When a transition is not permitted, the assistant asks whether it should walk the intermediate steps (New -> Triaged -> InProgress). |
| ⚠️ | `assign_ticket` | "Assign the ticket to Sarah" | InMemory | Assignment works. In one run the assistant claimed a valid agent did not exist, then listed the roster as "Alex Kim, Jordan Lee, Sam Patel, Priya Nair, Morgan Davis" and stated it had no tool to list agents. A retry with the same name succeeded.<br><br>**Cause:** [B4](#b4). "Morgan Diaz" exists (`AgentRoster.cs:18`); "Morgan Davis" does not - the model misreported the names from its own `tool_result`. Its statement about the missing tool is accurate: there is no `list_agents`, and the roster only reaches the model through the error path of `AssignTicketTool`. |
| | `route_ticket` | "Route the ticket to the right team" (ADR 0032) | InMemory | Not yet run. |
| ✅ | `add_comment` | "Comment: workaround is to restart" | InMemory | Comments are authored as the assistant.<br><br>**Not a bug, by design:** `ICurrentUser` is the demo seam (`DemoCurrentUser`, constant owner) and real auth is explicitly out of scope per `CLAUDE.md`. Once a login exists, only that seam is swapped. |
| ⏭️ | `remember` | "Remember: I prefer tickets in German" | Postgres + Voyage | Ran against `UnavailableMemoryStore` (InMemory), not pgvector - see [B8](#b8). Re-run required. |

### 1.2 Retrieval

| Status | Tool | Test prompt / trigger | Prerequisite | Comment (result / recommendation) |
|---|---|---|---|---|
| ✅ | `search_tickets` | "Which open tickets are there about VPN?" (SQL search) | InMemory | |
| ⏭️ | `find_similar_tickets` | Duplicate check, fires automatically before `create_ticket` | Postgres + Voyage | Ran against `UnavailableTicketSimilaritySearch` (InMemory) - see [B8](#b8). The pass confirmed honest degradation, not the duplicate check itself. Re-run required. |
| ⏭️ | `search_knowledge_base` | "How do I reset a password?" -> searches `./KnowledgeBase/*.md` (3 articles: password reset, VPN, new-hire hardware) | Postgres + Voyage | Reported the knowledge base as unreachable and answered from general knowledge, offering to file a ticket.<br><br>**Cause:** [B8](#b8), not a defect. The tool hit `UnavailableKnowledgeBaseSearch` on InMemory and degraded honestly per ADR 0029. Verified working on real Postgres afterwards (#192). Re-run as part of the RAG group. |
| ⏭️ | `recall_memory` | "What do you know about me?" | Postgres + Voyage | Same InMemory seam as `remember`. Re-run required. |

### 1.3 Evaluation

| Status | Tool | Test | Expectation | Comment (result / recommendation) |
|---|---|---|---|---|
| ⚠️ | `check_grounding` (ADR 0031) | Ask a question the knowledge base does not cover | The assistant says plainly that the answer is not grounded, and does not hallucinate | Behaved correctly, but was not exercised thoroughly, and the knowledge base it should have been grounding against was never loaded ([B8](#b8)). Re-run against real Postgres. |

### 1.4 Multi-step chain (ADR 0027)

| Status | Test prompt | Expected tool chain | Comment (result / recommendation) |
|---|---|---|---|
| ⚠️ | "The projector in the conference room has a display fault. Create a ticket, assign it to Morgan Diaz, set the priority to Critical, and it must be done by 15:00 today because there is an important meeting with an investor." | `find_similar_tickets` -> `create_ticket` -> `assign_ticket` in one turn | The chain runs and the ticket is created, assigned, prioritised and dated in a single turn. The duplicate-check link ran against the InMemory seam ([B8](#b8)), so the chain is confirmed but its first step is not. |

---

## 2. Conversation state (ADR 0026)

| Status | Test | Expectation | Comment (result / recommendation) |
|---|---|---|---|
| | Send the first turn | The `conversation` SSE event carries a `conversationId` | Not yet run. |
| | Second turn: send only the new user message plus `conversationId` | The server reloads the transcript | Not yet run. |
| ⚠️ | "Set the priority to Critical" without naming a ticket id | The assistant resolves the reference from the transcript | The assistant confirmed changes it had not made. On a repeated request the ticket was changed and the requested comment added.<br><br>**Cause:** [B1](#b1), the same defect as the `update_ticket` row. Reference resolution itself worked - the right ticket was hit in the end. |

The web UI has no resume UI yet, but both rows are testable with `curl` or the `.http` file.

---

## 3. Streaming ticket summaries (ADR 0033)

| Status | Test | Expectation | Comment (result / recommendation) |
|---|---|---|---|
| ⚠️ | `GET /api/v1/tickets/{id}/summary` | SSE stream, four sections | Works well, with useful hints. Timestamps are not converted into the user's timezone: a comment stamped 12:07 appears in the summary as 10:07.<br><br>**Cause:** [B5](#b5). The two-hour gap is exactly the CEST offset. |
| ✅ | Web: open ticket detail | The summary visibly streams in | |

---

## 4. AI insights dashboard (ADR 0034)

Endpoint: `GET /api/v1/dashboard/ai`, web: "AI Insights". Chat first, then load the page.

| Status | Metric (trailing 7 days) | Expectation | Comment (result / recommendation) |
|---|---|---|---|
| ✅ | Automation rate | Plausible after chat activity | |
| ✅ | Duplicate-check hit rate | Plausible | |
| ⚠️ | Retrieval confidence | Plausible | Reported as unavailable. **Not a separate bug**, a consequence of [B8](#b8). `AiDashboardAggregation.cs:21` feeds the metric from the `Retrieval` and `DuplicateCheck` kinds. Within `Retrieval`, only `search_knowledge_base` reports a score; `find_similar_tickets` reports `null` when it found no matches. Zero samples means `n/a` is the **correct** display. |
| ✅ | Tool call counts | Counts the tools actually fired | Displayed correctly, including the error column. |
| ✅ | Token usage | Plausible | |
| ✅ | Fresh start, no chat | Shows `n/a`, **not** `0 %` | |

---

## 5. Agent sandbox (ADR 0035) - guards to break

| Status | Guard | How to trigger | Expectation | Comment (result / recommendation) |
|---|---|---|---|---|
| | `KnownToolGuard` | internal only (unknown tool name) | The call is refused | |
| | `InputSizeGuard` | Throw in a huge description (`>MaxInputCharacters`) | The guard blocks | |
| | `WriteBudgetGuard` | "Create 30 tickets" | Stops after `MaxWritesPerTurn` | |
| | `RateLimitGuard` | Per-owner token bucket: lower `ToolCallsPerMinute` / `ModelTurnsPerMinute` and spam | The guard throttles | |
| | `HumanReviewGuard` | Only applies to the worker, never in chat | Never active in chat | |

Covered by automated tests in `tests/ServiceDeskLite.Tests.Api/Assistant/Sandbox/` (`ToolGuardTests`, `ToolGuardPipelineTests`, `HumanReviewGuardTests`).
Reproducing these by hand is confirmation rather than first coverage, so the priority is correspondingly low.

---

## 6. Autonomous ticket worker (ADR 0037)

```bash
AutonomousWorker__Enabled=true \
AutonomousWorker__ScanIntervalSeconds=30 \
AutonomousWorker__MinTicketAgeMinutes=0 \
dotnet run --project src/ServiceDeskLite.Api
```

| Status | Test | Expectation | Comment (result / recommendation) |
|---|---|---|---|
| | Worker scans tickets | Picks up `New` / `Triaged` / `InProgress` | |
| | Permitted unattended actions | Only `add_comment` plus status -> `Triaged` / `Waiting` | |
| | Forbidden actions | No `Resolve`, no `Close` | |
| | Create a ticket, wait, inspect the audit log | Actor is `ai-assistant` | |

Covered by automated tests in `tests/ServiceDeskLite.Tests.Evaluation/Scenarios/AutonomousWorkerTests.cs` and `tests/ServiceDeskLite.Tests.Api/Worker/AutonomousWorkerOptionsTests.cs`.

---

## 7. Honest degradation

Remove the Voyage key or run on InMemory. This is a feature, not a bug.

| Status | Tool | Expectation without Voyage | Comment (result / recommendation) |
|---|---|---|---|
| ✅ | `find_similar_tickets` | Reports `unavailable`, no fake empty results | |
| ✅ | `search_knowledge_base` | Reports `unavailable` | |
| ✅ | `remember` | Reports `unavailable` | |
| ✅ | `recall_memory` | Reports `unavailable` | |

Passed by accident: because the Postgres stack never started ([B8](#b8)), the entire RAG group ran against the unavailable seams and every tool degraded honestly.
Worth keeping as a deliberate scenario rather than a lucky one.

---

## 8. Retry (ADR 0027)

`ToolRetryPolicy`: Voyage 429/5xx and timeouts get 2 retries with 200ms x 2^n backoff.
Deterministic failures (validation) surface immediately as `is_error` so the model can correct itself.

| Status | Failure class | Test | Expectation | Comment (result / recommendation) |
|---|---|---|---|---|
| | Transient (Voyage 429/5xx, timeout) | Force the failure | 2 retries, 200ms x 2^n backoff | |
| | Deterministic (validation) | "Priority Supercritical" | Immediate `is_error` to the model, model self-corrects, no retry | |

`ToolRetryPolicy` is covered by `tests/ServiceDeskLite.Tests.Api/Assistant/ToolRetryPolicyTests.cs`.

---

## 9. Web client

| Status | Test | Expectation | Comment (result / recommendation) |
|---|---|---|---|
| ✖️ | The assistant answers in Markdown | The web client renders it formatted | Markdown is shown as raw text. **Cause:** [B7](#b7). |

---

## Root-cause analysis

Traced into the code after the manual pass.
Of eight findings, four are genuine defects, one is a downstream symptom, one is intended design, one is model behaviour that ADR 0038 predicted as a cost, and one is an environment failure that silently invalidated a whole test group.

### B1

Issue: #190

**The assistant confirms changes it never made.** Affects `update_ticket` (1.1) and conversation state (2) - one defect, not two.

The agent loop is not at fault: `AgentLoop.cs:181` iterates only on `StopReason.ToolUse` and stops otherwise.
No path swallows a tool call. The model produced text and asserted the outcome.

ADR 0038 names the trade-off itself: with thinking disabled, Sonnet 5 reaches less readily for tools it was not explicitly asked to call.
The observed behaviour is the harsher variant: not merely skipping the tool, but claiming its result anyway.
A user is told the workspace changed when it did not, which is a different thing from answering with less tool enthusiasm.

**Severity:** high. The assistant's core promise is that it actually acts.

### B2

Issue: #191

**`search_knowledge_base` reports technical failures as success.** `SearchKnowledgeBaseTool.cs:152-157` returns `IsError: false` from its catch branch, with no `MatchCount` and no `SemanticAvailable`.

A failing Postgres query or a Voyage error is then indistinguishable from a successful search for every consumer: the dashboard counts a clean invocation, `ToolRetryPolicy` sees nothing to classify, and "unavailable" collapses into "failed".
`FindSimilarTicketsTool.cs:212` handles the identical case correctly with `IsError: true`. The inconsistency is the defect.

**Severity:** medium. Latent, not observed: the test pass never reached the catch branch (see [B8](#b8)). The first real Postgres or Voyage failure will be reported as a success.

### B3

Issue: #192 - **not a bug, refuted.**

Originally filed as "`search_knowledge_base` returns nothing on Postgres + Voyage".
The premise was wrong: the stack never ran (see [B8](#b8)), and the test hit `UnavailableKnowledgeBaseSearch` on InMemory.
The assistant's message was correct behaviour under ADR 0029.

Verified on real Postgres after the port fix: 10 chunks across 3 articles embedded, `search_knowledge_base` returns 4 passages, and the `citation` event carries the right article.
The issue was repurposed into that verification and closed.

### B4

Issue: #193

**The model has no access to the agent roster.** It only reaches the model through the error path of `AssignTicketTool.cs:110-114`; there is no `list_agents` tool.

"Morgan Diaz" exists (`AgentRoster.cs:18`, and is the routing target for `TicketCategory.Account` in `KeywordTicketRouter.cs:44`).
"Morgan Davis" does not - the model misreported the names from its own `tool_result`.
The same underlying failure as [B1](#b1), visible here in a harmless place.

### B5

Issue: #194

**Summary timestamps are rendered in UTC instead of the user's timezone.** `TicketSummaryService.prompt.cs:98` formats every timestamp as UTC:

```csharp
value.ToString("yyyy-MM-dd HH:mm 'UTC'zzz", CultureInfo.InvariantCulture);
```

The chat path injects `Anthropic:UserTimeZone` into the system prompt; the summary path does neither.
The observed 12:07 against 10:07 is exactly the CEST offset.

### B6

Issue: #195

**The assistant asks again for a change it was already told to make.** Observed after the correction in 1.1.
A milder prompt-level issue, deliberately kept apart from [B1](#b1) so that fixing B1 by making the model more cautious does not quietly make this worse.

### B7

Issue: #196

**The web client does not render Markdown.** `AssistantChatPage.razor:38` emits `@entry.Text` raw and Blazor escapes it; no Markdown renderer is referenced in the web project.

Security-relevant when fixing: the ticket text in the chat originates from user input.
A `MarkupString` without prior sanitizing would be a stored XSS vector.

### B8

Issue: #197

**The docker stack never started on the test machine.** `docker-compose.yml` published two fixed host ports, `5432` and `8080`.
Both were held by a second compose project.
`db` failed to bind, and `api` waits behind `depends_on: condition: service_healthy`, so it never started either.

```
Bind for 0.0.0.0:5432 failed: port is already allocated
```

Evidence: both containers sat in `Created`, never `Up`, and the `servicedesklite_postgres_data` volume was empty - Postgres had never initialised.

**Consequence for this test plan:** every row with the prerequisite "Postgres + Voyage" in fact ran against InMemory on `:5300`, where `UnavailableTicketSimilaritySearch`, `UnavailableMemoryStore` and `UnavailableKnowledgeBaseSearch` are wired in.
The green marks on `find_similar_tickets`, `remember` and `recall_memory` therefore confirmed honest degradation, not the feature.
Section 7 was carried out by accident, and passed.

A silent test failure of this kind is more dangerous than a loud one: the plan looked green while half a feature group was never exercised.

---

## Overall verdict

| Area | Status | Recommendation |
|---|---|---|
| Chat + tools | ⚠️ | Tools execute correctly when they are called. The defect sits in the decision to call them ([B1](#b1)). Fix before release. |
| RAG (Postgres) | ✖️ | Never exercised ([B8](#b8)). The knowledge-base path was verified afterwards (#192); duplicate check and memory are still outstanding. |
| Conversation state | ⚠️ | Reference resolution from the transcript works. The anomaly is [B1](#b1), not the state. |
| Summaries | ⚠️ | Sound in substance. [B5](#b5) is small and isolated. |
| AI dashboard | ✅ | The missing retrieval confidence is correct behaviour at zero samples, downstream of [B8](#b8). |
| Guards | ⏭️ | Covered by automated tests. Manual reproduction optional. |
| Autonomous worker | ⏭️ | Covered by automated tests. |
| Degradation | ✅ | Carried out by accident: the whole RAG run hit the unavailable seams and reported honestly. |
| Retry | ⏭️ | Covered by automated tests. |
| Web client | ✖️ | [B7](#b7), including the sanitizing requirement. |

**Order of work:** [B8](#b8) is fixed and was the precondition for everything else - without a running stack the RAG group cannot be tested at all.
Then [B1](#b1), the only finding that actively tells users something false.
[B2](#b2) is latent but cheap, and belongs in before the next Postgres run.
[B5](#b5) and [B7](#b7) are independent, small, and can be slotted in at any time.
The RAG rows must be repeated against real Postgres before this plan counts as complete.

**Not filed as a bug:** comments authored as the assistant (1.1, `add_comment`).
`ICurrentUser` is the demo seam, and real auth is explicitly out of scope per `CLAUDE.md`.

**Still untested:** `route_ticket` end to end in chat (1.1), the first two conversation-state rows (the `conversation` SSE event and a second turn via `curl`), and the whole RAG group after the port fix.
