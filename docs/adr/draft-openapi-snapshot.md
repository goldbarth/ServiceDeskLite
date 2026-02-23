# ADR 000X: OpenAPI Snapshot Strategy

## Status

Accepted

## Context

The API exposes versioned endpoints (v1) as the contract boundary between
API and Web.

To ensure contract stability and visibility, the OpenAPI specification
must be versioned and verifiable.

## Decision

We introduce an OpenAPI snapshot strategy:

- The OpenAPI v1 spec is generated from the running API.
- The snapshot is stored at `docs/api/openapi.v1.json`.
- GitHub Pages renders the spec interactively using ReDoc.
- CI verifies that the snapshot does not drift unintentionally.

The ReDoc bundle is fetched during the documentation workflow
to keep the repository clean and deterministic.

## Consequences

### Positive

- Explicit contract artifact
- Contract drift detection
- Transparent API documentation
- Improved architectural clarity

### Negative

- Additional CI step
- Slightly more documentation complexity

## Notes

The OpenAPI snapshot is considered part of the public contract and must
be updated intentionally when breaking changes occur.
