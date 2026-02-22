#!/usr/bin/env bash
set -euo pipefail

# Generates docs/api/openapi.v1.json from the running API and normalizes it deterministically.

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
API_PROJECT="$ROOT_DIR/src/ServiceDeskLite.Api"
OUT_FILE="$ROOT_DIR/docs/api/openapi.v1.json"
TMP_RAW="$(mktemp)"
TMP_NORM="$(mktemp)"

HOST="127.0.0.1"
PORT="5238"
BASE_URL="http://${HOST}:${PORT}"
OPENAPI_URL="${BASE_URL}/openapi/v1.json"

cleanup() {
  if [[ -n "${API_PID:-}" ]] && kill -0 "$API_PID" 2>/dev/null; then
    kill "$API_PID" >/dev/null 2>&1 || true
  fi
  rm -f "$TMP_RAW" "$TMP_NORM"
}
trap cleanup EXIT

echo "==> Starting API..."
pushd "$ROOT_DIR" >/dev/null

# Run API on a deterministic URL/port. No rebuild inside script to keep it fast/predictable.
ASPNETCORE_URLS="${BASE_URL}" \
DOTNET_ENVIRONMENT="Development" \
dotnet run --project "$API_PROJECT" -c Release --no-build --no-launch-profile >/dev/null 2>&1 &
API_PID=$!

# Fail fast if the API process dies immediately (e.g., port bind denied).
sleep 0.5
if ! kill -0 "$API_PID" 2>/dev/null; then
  echo "ERROR: API process exited during startup."
  echo "Tip: run the API manually to see logs:"
  echo "  dotnet run --project src/ServiceDeskLite.Api -c Release --no-launch-profile -- --urls ${BASE_URL}"
  exit 1
fi

popd >/dev/null

echo "==> Waiting for OpenAPI endpoint: $OPENAPI_URL"
for i in {1..60}; do
  if curl --silent --fail "$OPENAPI_URL" >/dev/null; then
    break
  fi
  sleep 0.25
done

# If the endpoint never became available, abort with a clear error.
if ! curl --silent --fail "$OPENAPI_URL" >/dev/null; then
  echo "ERROR: OpenAPI endpoint did not become available: $OPENAPI_URL"
  exit 1
fi

echo "==> Downloading OpenAPI JSON..."
curl --silent --fail "$OPENAPI_URL" > "$TMP_RAW"

echo "==> Normalizing JSON deterministically..."
python3 - "$TMP_RAW" "$TMP_NORM" <<'PY'
import json, sys

src, dst = sys.argv[1], sys.argv[2]

with open(src, "r", encoding="utf-8") as f:
    data = json.load(f)

# Deterministic formatting:
# - sort_keys=True ensures stable object key order
# - separators for stable whitespace
# - ensure_ascii=False for stable UTF-8 output
text = json.dumps(
    data,
    ensure_ascii=False,
    sort_keys=True,
    indent=2,
)
text += "\n"

with open(dst, "w", encoding="utf-8", newline="\n") as f:
    f.write(text)
PY

mkdir -p "$(dirname "$OUT_FILE")"
mv "$TMP_NORM" "$OUT_FILE"

echo "==> Wrote snapshot: $OUT_FILE"
