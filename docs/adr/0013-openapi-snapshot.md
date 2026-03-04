# ADR 0013: OpenAPI Snapshot as a Committed Artifact with CI Drift Check

## Status

Accepted

## Context and Problem Statement

The API generates an OpenAPI specification at runtime via
`Microsoft.AspNetCore.OpenApi`. Without a committed snapshot, the spec exists
only transiently — it is invisible in pull requests, cannot be diffed, and
provides no signal when a contract change slips in unintentionally.

The question is: where does the OpenAPI specification live, and how does the
team know when it changes?

## Decision Drivers

- API contract changes must be visible and reviewable in pull requests,
  the same way source code changes are.
- The spec must be stable enough to commit: identical inputs must produce
  identical output across runs and platforms.
- The CI pipeline must catch unintentional drift (endpoint added, field
  renamed, type changed) before a PR is merged.
- The committed file must be usable as a documentation artifact without
  additional generation steps.

## Considered Options

### Option A — Committed snapshot with CI drift check (Selected)

The OpenAPI spec is generated from the running API by a shell script
(`generate-openapi.sh`), normalised deterministically, and committed to
`docs/api/openapi.v1.json`. CI regenerates it on every push and fails
the build if `git diff --exit-code` detects any change.

### Option B — Generated at deploy time only

The spec is produced during deployment and published to a hosting target.
No committed file, no diff in PRs. Contract changes are only visible after
deployment, and only to someone who compares two deployed versions.

### Option C — External API documentation service

Push the spec to an external platform (Stoplight, Readme.io). Keeps the
repository clean but introduces an external dependency, requires credentials
in CI, and still does not answer the drift-detection question.

## Decision Outcome

Chosen option: **Option A — Committed snapshot with CI drift check**.

### Why this trade-off makes sense for this project

- **Contract changes become first-class PR events.** When an endpoint is
  added or a field type changes, the spec diff appears alongside the code
  diff. Reviewers see the impact on the public contract without running
  the API.
- **Deterministic normalisation eliminates diff noise.** The raw spec from
  `Microsoft.AspNetCore.OpenApi` is fed through a Python normalisation step:
  `sort_keys=True`, consistent indentation, LF line endings, no BOM. Without
  this, key ordering or whitespace differences between runs would cause
  spurious drift failures.
- **The drift check is a compile-time gate, not a post-deploy observation.**
  Unintentional contract changes are caught when the PR is raised, not after
  the API is deployed.
- **The Linux-only constraint is acceptable.** The generation script requires
  bash and is gated with `if: runner.os == 'Linux'` in CI. The Windows matrix
  leg still runs build and tests — it just skips the snapshot step.

### How the snapshot pipeline works

```
CI (Linux only)
  └── generate-openapi.sh
        ├── dotnet run (Release, no launch profile, InMemory)
        ├── curl /openapi/v1.json  →  raw JSON
        ├── python3: sort_keys, indent=2, LF, no BOM
        └── write  docs/api/openapi.v1.json
  └── git diff --exit-code docs/api/openapi.v1.json
        └── non-zero exit  →  build fails with instructions
```

A separate manual workflow (`openapi-snapshot.yml`) runs on `workflow_dispatch`
and uploads the snapshot as a CI artifact — useful for updating the committed
file without a local API run.

## Consequences

### Positive Consequences

- The committed spec is the single source of truth for the current API
  contract — no need to run the API to know what it exposes.
- Drift is caught immediately in PR CI, not after merge.
- The snapshot feeds directly into the documentation site (ReDoc on
  GitHub Pages) without a separate generation step in the docs workflow.

### Negative Consequences

- The snapshot file must be regenerated and committed whenever the API
  contract changes intentionally. Forgetting this step causes the next
  CI run to fail.
- Generation requires starting the API process in CI, which adds a small
  amount of time to the Linux build leg and requires the API to be
  startable in a headless environment.
- The generation script is bash-only and therefore skipped on the Windows
  CI leg, leaving a small platform asymmetry in the CI matrix.

## Re-evaluation Triggers

Revisit when:

1. The API gains multiple spec versions (v2, v3) — the snapshot strategy
   and the CI script need to cover multiple output files.
2. The generation time becomes significant enough to warrant a dedicated
   CI job rather than an inline step in the main build.

## Related

- ADR 0011 – OpenAPI via `Microsoft.AspNetCore.OpenApi` + Swagger UI
- `.build/docs/generate-openapi.sh`
- `.github/workflows/ci.yml` – drift check steps
- `.github/workflows/openapi-snapshot.yml` – manual snapshot workflow
- `docs/api/openapi.v1.json` – committed snapshot artifact
