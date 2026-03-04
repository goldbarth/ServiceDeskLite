# ADR 0016: `packages.lock.json` per Project for Reproducible Restores

## Status

Accepted

## Context and Problem Statement

NuGet package resolution is version-range-based by default. A `PackageReference`
of `Version="10.0.*"` resolves to whatever the highest matching version is at
the time of restore. On a clean CI machine or a new developer checkout,
`dotnet restore` may resolve different versions than the last working build
if a new patch release has been published. The result is a build that compiles
and tests successfully locally but fails in CI, or vice versa, with no
visible change in the source tree.

## Decision Drivers

- Every `dotnet restore` must produce the exact same package graph,
  regardless of when or where it runs.
- CI build times must be minimised: unchanged dependency sets should not
  trigger a full package download on every run.
- The package graph must be auditable: a reviewer must be able to see
  exactly which versions are in use by reading committed files.
- The lock file mechanism must apply to all projects uniformly, without
  per-project opt-in.

## Considered Options

### Option A — Committed `packages.lock.json` per project via `Directory.Build.props` (Selected)

`RestorePackagesWithLockFile=true` is set once in `Directory.Build.props`,
which propagates the setting to every project in the solution. Each project
generates its own `packages.lock.json` that captures the fully resolved
dependency graph including transitive packages. All lock files are committed.
CI uses the lock files as the cache key for the NuGet package cache.

### Option B — No lock files; rely on floating version ranges

The simplest approach — no extra files, no maintenance overhead. But package
resolution is non-deterministic across time: a patch release published
overnight can change the restore result between a developer's machine and
the next CI run. Dependency surprises are discovered at build time, not
at the time of intentional update.

### Option C — Central `Directory.Packages.props` with `ManagePackageVersionsCentrally`

Pin all versions centrally in one file. Eliminates per-project version
declarations but does not prevent transitive version resolution from varying.
Lock files and central version management are complementary — not
mutually exclusive — but central management alone does not guarantee
restore reproducibility.

## Decision Outcome

Chosen option: **Option A — Committed `packages.lock.json` per project**.

### Why this trade-off makes sense for this project

- **`Directory.Build.props` applies the setting uniformly.** One property,
  all projects covered. No per-project opt-in required, no risk of a new
  project being added without lock file protection.
- **Per-project lock files reflect actual dependency graphs.** Each project
  has a different transitive closure. A solution-level lock file does not
  exist in NuGet — per-project is the correct granularity.
- **CI cache keying on lock files is effective.** The `actions/setup-dotnet`
  step uses `cache-dependency-path: **/packages.lock.json` as the cache key.
  When no lock file changes, the full NuGet package cache is restored from
  the cache layer in seconds rather than downloaded fresh.
- **Committed lock files make dependency changes visible in PRs.** When a
  package is updated, the lock file diff shows every transitive package that
  changed — not just the direct reference. This is the same principle as the
  OpenAPI snapshot: intentional changes are explicit, unintentional changes
  are caught.

### Maintenance requirement

Lock files must be regenerated after any package addition or update:

```
dotnet restore ./ServiceDeskLite.slnx
```

The updated lock files must be committed alongside the `PackageReference`
change. A CI restore that detects a lock file mismatch will fail with a
clear error (`NU1004`), making the missing commit immediately apparent.

## Consequences

### Positive Consequences

- Package resolution is identical on every machine and every CI run.
- Dependency changes are always visible as committed file changes.
- CI restore time on unchanged dependency sets is near-zero (cache hit).

### Negative Consequences

- Every `PackageReference` change requires a subsequent `dotnet restore`
  and a commit of the updated lock files. Forgetting this step causes a
  CI failure.
- Lock files contain the full transitive graph and can be large and noisy
  in diffs — reviewers may skip them. The important signal (which direct
  packages changed) is in the `.csproj`; the lock file diff is secondary.

## Re-evaluation Triggers

Revisit when:

1. Central package version management (`Directory.Packages.props`) is
   introduced — evaluate combining it with lock files for maximum
   reproducibility.

## Related

- `Directory.Build.props` — `RestorePackagesWithLockFile=true`
- `.github/workflows/ci.yml` — `cache-dependency-path: **/packages.lock.json`
- `src/**/packages.lock.json` — one per project
