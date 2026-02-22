# OpenAPI

This project publishes an OpenAPI v1 contract.

- **Snapshot (committed):** `openapi.v1.json`
- **Purpose:** Stable, versioned API contract for review, CI drift detection, and potential client generation.

## Snapshot

- [Download `openapi.v1.json`](openapi.v1.json)

## Notes

- The snapshot is generated from the running API and normalized deterministically.
- CI fails if the API contract changes without updating the snapshot.
