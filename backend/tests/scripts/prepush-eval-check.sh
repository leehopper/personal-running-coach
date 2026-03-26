#!/usr/bin/env bash
set -euo pipefail

# Pre-push eval cache check: runs Replay mode only, matching CI behavior.
# If cache is stale, blocks push and tells the user to re-record manually.

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
BACKEND_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"

cd "$BACKEND_DIR"

echo "[eval-cache] Verifying cache in Replay mode..."
if EVAL_CACHE_MODE=Replay dotnet test RunCoach.slnx --no-restore --filter "Category=Eval" 2>/dev/null; then
    echo "[eval-cache] Replay passed — cache is CI-stable."
    exit 0
fi

echo ""
echo "BLOCKED: Eval cache has stale or missing entries."
echo "Fix: run backend/tests/scripts/rerecord-eval-cache.sh"
echo "Then: git add backend/tests/eval-cache/ && git commit"
exit 1
