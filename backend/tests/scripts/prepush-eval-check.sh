#!/usr/bin/env bash
set -euo pipefail

# Pre-push eval cache check: runs Replay mode only, matching CI behavior.
# If cache is stale, blocks push and tells the user to re-record manually.

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
BACKEND_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"

cd "$BACKEND_DIR"

echo "[eval-cache] Verifying cache in Replay mode..."
# Invoke the test binary directly (matches lefthook dotnet-test pattern).
# `dotnet test` on SDK 10+ either hangs (VSTest routing against MTP-only
# xunit v3 projects on macOS) or silently ignores VSTest filter syntax
# (running ALL tests including non-Eval ones under EVAL_CACHE_MODE=Replay,
# which then erroneously fails). The binary's xunit v3 native CLI accepts
# `-trait "Category=Eval"` as the inclusion filter and runs the 15 Eval
# tests in ~2s.
TEST_BIN="$BACKEND_DIR/tests/RunCoach.Api.Tests/bin/Debug/net10.0/RunCoach.Api.Tests"
if [ ! -x "$TEST_BIN" ]; then
    dotnet build RunCoach.slnx --no-restore >/dev/null
fi
if EVAL_CACHE_MODE=Replay "$TEST_BIN" -trait "Category=Eval"; then
    echo "[eval-cache] Replay passed — cache is CI-stable."
    exit 0
fi

echo ""
echo "BLOCKED: Eval cache has stale or missing entries."
echo "Fix: run backend/tests/scripts/rerecord-eval-cache.sh"
echo "Then: git add backend/tests/eval-cache/ && git commit"
exit 1
