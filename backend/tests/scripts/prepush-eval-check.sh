#!/usr/bin/env bash
set -euo pipefail

# Smart pre-push eval cache check (DEC-039 compliant):
#
# 1. Run Replay mode — if green, cache is CI-stable, done.
# 2. If Replay fails and API key available — auto re-record + TTL fix.
# 3. If cache files changed after re-record — BLOCK push, tell user to commit.
# 4. If no API key and Replay fails — BLOCK, tell user to re-record.

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
BACKEND_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
CACHE_DIR="$BACKEND_DIR/tests/eval-cache"

cd "$BACKEND_DIR"

# Step 1: Try Replay mode
echo "[eval-cache] Verifying cache in Replay mode..."
if EVAL_CACHE_MODE=Replay dotnet test RunCoach.slnx --no-restore --filter "Category=Eval" 2>/dev/null; then
    echo "[eval-cache] Replay passed — cache is CI-stable."
    exit 0
fi

echo "[eval-cache] Replay failed — cache has stale or missing entries."

# Step 2: Check if we can auto-heal
HAS_KEY=false
if dotnet user-secrets list --project "$BACKEND_DIR/src/RunCoach.Api" 2>/dev/null | grep -q "Anthropic:ApiKey"; then
    HAS_KEY=true
fi

if [ "$HAS_KEY" = false ]; then
    echo ""
    echo "ERROR: Eval cache is stale and no API key available to re-record."
    echo "Fix: run backend/tests/scripts/rerecord-eval-cache.sh"
    exit 1
fi

# Step 3: Auto re-record
echo "[eval-cache] API key found — re-recording stale entries..."
rm -rf "$CACHE_DIR"

EVAL_CACHE_MODE=Record dotnet test RunCoach.slnx --no-restore --filter "Category=Eval"

# Step 4: Post-process TTL
echo "[eval-cache] Extending TTL to 9999-12-31..."
while IFS= read -r -d '' entry_file; do
    python3 -c "
import json
with open('$entry_file', 'r') as f:
    data = json.load(f)
data['expiration'] = '9999-12-31T23:59:59Z'
with open('$entry_file', 'w') as f:
    json.dump(data, f, indent=2)
"
done < <(find "$CACHE_DIR" -name "entry.json" -print0)

# Step 5: Verify Replay works now
echo "[eval-cache] Verifying re-recorded cache in Replay mode..."
if ! EVAL_CACHE_MODE=Replay dotnet test RunCoach.slnx --no-restore --filter "Category=Eval" 2>/dev/null; then
    echo ""
    echo "ERROR: Replay still fails after re-recording. Investigate manually."
    exit 1
fi

# Step 6: Check if files changed — if so, block push
CACHE_CHANGES=$(git status --porcelain "$CACHE_DIR" 2>/dev/null | wc -l | tr -d ' ')
if [ "$CACHE_CHANGES" -gt 0 ]; then
    echo ""
    echo "BLOCKED: Eval cache was auto-updated ($CACHE_CHANGES files changed)."
    echo "The cache is now correct but you need to commit it before pushing."
    echo ""
    echo "  git add backend/tests/eval-cache/"
    echo "  git commit -m 'chore: re-record eval cache fixtures'"
    echo "  git push"
    echo ""
    exit 1
fi

echo "[eval-cache] Cache is clean and CI-stable."
