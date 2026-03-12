# ADR 0017: Cross-Platform CI Matrix (Ubuntu + Windows)

## Status

Accepted

## Context and Problem Statement

The CI pipeline runs on GitHub Actions. A choice must be made about which
operating systems to include in the build and test matrix. Running on a
single platform is simpler and faster; running on multiple platforms catches
issues that only manifest on specific operating systems.

The project uses PostgreSQL with EF Core (connection handling, migrations),
Serilog (file sink path separators in future), and a bash-based OpenAPI snapshot
script — all of which have platform-specific surface area.

## Decision Drivers

- Path separator differences (`/` vs `\`) must not silently break file
  operations on either platform.
- PostgreSQL and EF Core behaviour must be verified on both Linux and Windows,
  since the application targets both as deployment platforms.
- A failing test on one platform must not silently pass on the other —
  full signal from both matrix legs is more useful than a fast fail on one.
- The OpenAPI snapshot generation script (`generate-openapi.sh`) is
  bash-only and cannot run on Windows.

## Considered Options

### Option A — Matrix on `ubuntu-latest` and `windows-latest`, `fail-fast: false` (Selected)

Both platforms run `restore → build → test` in parallel. The OpenAPI drift
check is gated to Linux only (`if: runner.os == 'Linux'`). `fail-fast: false`
ensures both legs run to completion regardless of individual failures,
giving full diagnostic signal from every push.

### Option B — Linux only

Single platform, faster CI, simpler configuration. Sufficient for most
.NET projects targeting Linux deployment. Misses Windows-specific issues
(path separator handling, line-ending behaviour in generated files, Windows
SQLite driver behaviour) until they surface in production or a contributor's
local environment.

### Option C — Matrix with `fail-fast: true` (default)

Same platforms as Option A but with the default `fail-fast` behaviour:
if one leg fails, GitHub Actions cancels the other. Saves compute minutes
when a failure is detected early, but discards the diagnostic output from
the surviving leg — making it harder to determine whether the failure is
platform-specific or universal.

## Decision Outcome

Chosen option: **Option A — Ubuntu + Windows matrix, `fail-fast: false`**.

### Why this trade-off makes sense for this project

- **Windows is a realistic developer environment.** Contributors may run
  the project natively on Windows. A CI signal from `windows-latest` gives
  early warning before a path or line-ending issue reaches a developer's
  machine as a confusing local failure.
- **`fail-fast: false` preserves diagnostic signal.** If the Windows leg
  fails and the Linux leg is cancelled, it is unknown whether the failure
  is Windows-specific or universal. With `fail-fast: false`, both results
  are always available on the same commit — it is immediately clear whether
  a failure affects one platform or both.
- **The OpenAPI drift check is Linux-only by design.** The generation script
  (`generate-openapi.sh`) requires bash, `curl`, and `python3` — all
  available on the Ubuntu runner but not reliably on Windows. The step is
  gated with `if: runner.os == 'Linux'` rather than duplicated or replaced
  with a PowerShell equivalent. The Windows leg still provides full build
  and test coverage.

### CI matrix summary

| Step                     | Ubuntu | Windows |
|--------------------------|--------|---------|
| Restore                  | ✓      | ✓       |
| Build                    | ✓      | ✓       |
| Generate OpenAPI snapshot| ✓      | —       |
| Fail if snapshot drifted | ✓      | —       |
| Test                     | ✓      | ✓       |

## Consequences

### Positive Consequences

- Platform-specific issues (path separators, PostgreSQL connection handling,
  EF Core behaviour) are caught on every push, not just when someone
  happens to run on that platform.
- Both matrix legs always run to completion — a single push always
  produces a full pass/fail signal for both platforms.

### Negative Consequences

- CI cost is roughly doubled compared to a single-platform run. Each push
  consumes GitHub Actions minutes on two runners in parallel.
- The Windows runner is generally slower than the Ubuntu runner for .NET
  builds, making it the bottleneck for the overall CI duration.
- The OpenAPI drift check is an asymmetry in the matrix: Linux has one
  additional step that Windows does not. A drift failure only blocks
  merge via the Linux leg.

## Re-evaluation Triggers

Revisit when:

1. macOS coverage becomes relevant (e.g. contributors filing macOS-specific
   issues) — add `macos-latest` to the matrix.
2. The OpenAPI snapshot script is rewritten in a cross-platform tool
   (e.g. a .NET global tool) — the Linux-only gate can be removed.

## Related

- ADR 0013 – OpenAPI Snapshot (Linux-only drift check)
- ADR 0016 – `packages.lock.json` Lock Files (CI cache strategy)
- `.github/workflows/ci.yml`
