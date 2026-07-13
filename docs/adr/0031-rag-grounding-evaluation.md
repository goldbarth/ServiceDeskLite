# ADR 0031: RAG Grounding Evaluation (deterministic self-check tool)

## Status

Accepted. The scoring decision below (a lexical evaluator) is superseded by ADR 0040, which grades by embedding similarity and keeps the lexical evaluator as the fallback. The tool, its streaming shape, and `IRagRetrievalContext` are unchanged.

## Context and Problem Statement

Knowledge-base RAG (ADR 0029) lets the assistant answer how-to questions from
retrieved passages and cite them. Nothing yet checks that the answer is *actually
supported* by those passages — the model can cite a source and still assert a claim
the source never made (a grounded-looking hallucination). Issue #158 asks to measure
grounding per RAG answer and have the agent self-correct when it is weak, with the
evaluation runnable against deterministic fixtures.

Decisions:

1. **How is grounding scored** — an LLM judge, or a deterministic function?
2. **How does the agent self-correct** without breaking token streaming?
3. **What does the check compare against** — and how does it get the sources?

## Decision Drivers

- **Deterministic, fixture-testable evaluation** (explicit acceptance criterion): the
  score must be reproducible in the test suite with no model or embedding call.
- **Streaming must stay intact** — the app streams tokens live (a load-bearing design
  value); an answer must not be buffered until verified, nor streamed twice.
- **Honest**: never claim grounding that was not measured against the real sources.
- **Fits the existing loop** — self-correction already happens via tool results that
  the model reads and acts on (ADR 0028).

## Decision Outcome

**Scoring: a deterministic lexical evaluator** (`GroundingEvaluator`). Each substantive
sentence of the answer is "supported" when at least half of its content words appear
in the combined source vocabulary; the score is the fraction of supported sentences,
banded into `Grounded` (≥ 0.6), `Weak` (≥ 0.3), `Ungrounded` (< 0.3). Unsupported
sentences are returned so the model sees exactly what to fix. No LLM judge in the core:
the check is reproducible and unit-tested against fixtures.

**Self-correction: a `check_grounding` tool the model calls on its draft, before
answering.** The prompt directs the model, after `search_knowledge_base`, to pass its
drafted answer to `check_grounding`; a weak/ungrounded result comes back with the
unsupported statements and an instruction to re-retrieve, drop the claims, or hedge.
Because the *draft* goes to the tool (tool inputs are not streamed) and only the
verified answer is emitted as text, **streaming is untouched and the user never sees a
retracted answer** — the key reason this beats a post-hoc server gate, which would
either stream a wrong answer first or buffer to avoid it.

**Sources: a per-request retrieval context** (`IRagRetrievalContext`, scoped).
`search_knowledge_base` records the passages it returned; `check_grounding` scores the
draft against exactly those, so the model does not re-pass sources (lossy) and the
check cannot be fooled by sources that were never retrieved. Each turn starts empty.

The grounding score also rides the tool_result SSE event (`confidence`), which the web
client renders as a grounding badge — visible evaluation without new stream plumbing.

## Consequences

- **Positive:** every KB-grounded answer is measured against its real sources; the
  agent self-corrects in-loop on weak grounding; the evaluator is deterministic and
  fixture-tested; streaming and the citation flow are untouched; no new datastore.
- **Negative / limitations:** the check is **lexical**, so it assumes the answer and
  sources share a language — a German answer over English sources would score falsely
  low (cross-lingual grounding would need embeddings, deferred). It rewards vocabulary
  overlap, so a fluent paraphrase can score lower than a copy-paste; the bands are
  hand-tuned. Self-correction relies on the model calling the tool (prompt-driven, like
  the other tools); the deterministic evaluator is validated independently regardless.
- **Deliberately cut:** embedding/NLI-based entailment scoring, an LLM judge, and a
  hard server-side gate that blocks ungrounded answers (kept advisory to preserve the
  edge-adapter stance — the model decides, guided by the tool result).

## Related

- ADR 0029 - Knowledge-Base RAG (the retrieval + citation this verifies)
- ADR 0028 - Self-critique and confidence (the in-loop self-correction pattern)
- ADR 0023 - AI Assistant as Edge Adapter (tools guide, never replace, the model)
- `src/ServiceDeskLite.Api/Assistant/GroundingEvaluator.cs`
- `src/ServiceDeskLite.Api/Assistant/CheckGroundingTool.cs`
