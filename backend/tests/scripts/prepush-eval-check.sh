#!/usr/bin/env bash
set -euo pipefail

# Pre-push eval cache check: runs Replay mode only, matching CI behavior.
# If cache is stale, blocks push and tells the user to re-record manually.

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
BACKEND_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"

cd "$BACKEND_DIR"

echo "[eval-cache] Verifying cache in Replay mode..."
# MTP-native syntax (.NET SDK 10+ via global.json test.runner): xunit v3
# uses --filter-trait, not the legacy VSTest --filter "Category=X" syntax
# (which MTP silently ignores — running ALL tests including non-Eval ones
# under EVAL_CACHE_MODE=Replay, which then erroneously fails).
if EVAL_CACHE_MODE=Replay dotnet test --solution RunCoach.slnx --filter-trait "Category=Eval" --report-trx 2>/dev/null; then
    echo "[eval-cache] Replay passed — cache is CI-stable."
    exit 0
fi

echo ""
echo "BLOCKED: Eval cache has stale or missing entries."
echo "Fix: run backend/tests/scripts/rerecord-eval-cache.sh"
echo "Then: git add backend/tests/eval-cache/ && git commit"
exit 1
