# ADR 0040: Semantic Grounding Evaluation (embeddings, lexical fallback)

## Status

Accepted. Supersedes the scoring decision of ADR 0031; the tool, the streaming shape, and the per-request `IRagRetrievalContext` from that ADR are unchanged.

## Context and Problem Statement

ADR 0031 scored grounding with a deterministic lexical evaluator: a sentence counts as supported when enough of its content words appear in the retrieved passages.
That was the right first move - it is reproducible, free, and fixture-testable, which was an explicit acceptance criterion.
It is also lexical, which is where it is wrong about the things that matter.

Issue #188 measured it against the passage in `RagGroundingTests`:

| Answer | Verdict |
|---|---|
| Verbatim restatement of the passage | Grounded |
| A hallucination | Ungrounded |
| A correct paraphrase | Ungrounded |
| A correct answer in German, against an English passage | Ungrounded |
| The exact hedge the prompt asks for when sources are thin | Ungrounded |

It catches the hallucination.
It also fails a correct paraphrase, a correct translation, and the hedging behaviour the system prompt demands - each scores near zero, because word overlap sees no shared words.

Two things turned this from a latent quirk into a blocker.

ADR 0039 made the grounding check a mechanism: it is now forced after a retrieval, not left to the prompt.
Forcing a check whose detector fails correct answers feeds those false alarms to the model on every grounded answer.

And any move to surface the score - to a user, or to a dashboard, or behind a threshold - would turn the same false alarms into noise a person learns to ignore.
A threshold does not rescue it: a correct paraphrase scores 0.00, not 0.55, so no cut-off separates it from a hallucination.

## Decision Drivers

- **A correct paraphrase must ground.** The prompt tells the model to answer in the user's language and in its own words; the detector has to agree that a faithful restatement is supported.
- **Cross-lingual grounding must work.** A German answer against an English knowledge base is the normal case here, not an edge one.
- **The check still has to return a verdict when embeddings are gone.** It is enforced now (ADR 0039); a check that can silently produce nothing is back to being no check.
- **Testable without a live model.** The semantic path cannot be bit-reproducible, but its logic has to be assertable deterministically.
- **One embedding call per answer.** The cost has to stay proportionate to a single grounding check.

## Decision Outcome

**Grounding is scored by meaning, behind an `IGroundingEvaluator` port with two implementations.**

`SemanticGroundingEvaluator` is the default.
It embeds the answer's substantive sentences together with the retrieved passages in one Voyage call, then, for each sentence, takes the cosine similarity of its nearest passage.
A sentence counts as supported when that similarity clears `SemanticSupportThreshold` (0.6, calibrated for voyage-3.5).
The score stays what it was - the fraction of substantive sentences supported - and the verdict bands are unchanged (`GroundingEvaluator.VerdictFor`).
Only how one sentence is judged supported moves from word overlap to vector proximity.
That single change is what makes a paraphrase and a translation ground: they sit close to the passage they restate, where the words did not match.

**Sentences and passages are embedded in one call, both as documents.**
Voyage prefixes queries and documents differently, which would split the work into two calls.
For a symmetric similarity between a sentence and a passage the shared `document` type is enough, and one call keeps the cost at the "one embedding call per answer" the driver asks for.

**The lexical evaluator survives as `LexicalGroundingEvaluator`, and it is the fallback.**
When no Voyage key is configured, or an embedding call fails, the semantic evaluator delegates to it rather than returning nothing.
In practice a grounding check only runs after a knowledge-base retrieval, and retrieval already needs Voyage, so the fallback is the transient-failure net and the InMemory path, not the common one.
The original ADR 0031 evaluator, unchanged, is what runs there - so its deterministic fixture tests keep meaning.

**Testability.**
The lexical path stays fixture-tested with no call, exactly as before.
The semantic path is tested with a fake `IEmbeddingClient` that returns vectors by meaning: the paraphrase and the German answer are given the passage's direction, the hallucination an orthogonal one, and cosine then does what real embeddings would.
The threshold logic, the per-sentence unsupported list, and all three fallback triggers are asserted deterministically.

Rejected: **an LLM judge.**
It grades paraphrase and language well, at the cost of another model call per answer, non-determinism, and a second model whose behaviour drifts per release - the property ADR 0039 spent effort removing.

Rejected: **keeping the lexical evaluator as the primary.**
It is the thing issue #188 measured as wrong on correct answers.

Rejected: **embedding the whole answer as one vector.**
It gives a single score but loses the per-sentence breakdown, and the unsupported-sentence list is what the tool hands back to the model to fix.

## Consequences

- **The false alarms issue #188 measured are addressed at the source.** A correct paraphrase, a correct translation, and a correct hedge now ground where embeddings are present.
- **Surfacing the verdict becomes defensible.** ADR 0039 deliberately kept the score away from users and dashboards while the detector was lexical. That objection is resolved for the semantic path. Whether to surface it is a separate product decision and is not taken here.
- **The check is no longer deterministic on the semantic path.** Two runs of the same answer can differ if the embeddings differ. The verdict bands and the fallback keep the behaviour stable enough to reason about, and the fixtures pin the logic, but exact reproducibility now belongs only to the lexical path.
- **The threshold is a calibration surface.** `SemanticSupportThreshold` is tuned for voyage-3.5. A model change is the one thing that moves it, which is why it is a documented const and not configuration.
- **Whole-answer scoping (issue #188, problem 2) is still open.** Greetings, follow-up questions and ticket confirmations are not claims about the sources, yet the substantive-sentence heuristic still counts any sentence with content words. It does not bite while `check_grounding` receives only the draft the model chose to submit; classifying claim from non-claim is deferred until something grades a finished answer.

## Related

- ADR 0031: the grounding self-check this re-scores. The tool, its streaming shape, and `IRagRetrievalContext` are inherited unchanged; only the evaluator behind them is replaced.
- ADR 0039: the enforcement that made the detector's quality load-bearing, and that gated surfacing on this work.
- ADR 0029: knowledge-base retrieval and the Voyage embedding client this reuses.
- Issue #188: the measurement that motivated the replacement.
