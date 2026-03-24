#!/usr/bin/env bash
set -euo pipefail

# Re-records eval cache fixtures from scratch following DEC-039 workflow:
#   1. Delete existing cache (purge stale entries)
#   2. Record fresh responses via Anthropic API
#   3. Post-process entry.json files to 9999-12-31 TTL
#   4. Verify Replay mode works with new fixtures
#
# Usage: ./backend/tests/scripts/rerecord-eval-cache.sh
# Requires: Anthropic API key configured via dotnet user-secrets

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
BACKEND_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
CACHE_DIR="$BACKEND_DIR/tests/eval-cache"

echo "=== Eval Cache Re-Recording (DEC-039) ==="
echo ""

# Step 0: Verify API key is available
echo "[1/5] Checking API key..."
if ! dotnet user-secrets list --project "$BACKEND_DIR/src/RunCoach.Api" 2>/dev/null | grep -q "Anthropic:ApiKey"; then
    echo "ERROR: No Anthropic API key found in user-secrets."
    echo "Set it with: dotnet user-secrets set \"Anthropic:ApiKey\" \"<key>\" --project backend/src/RunCoach.Api"
    exit 1
fi
echo "  API key found."

# Step 1: Delete existing cache
echo "[2/5] Deleting existing cache at $CACHE_DIR..."
rm -rf "$CACHE_DIR"
echo "  Deleted."

# Step 2: Record fresh
echo "[3/5] Recording fresh eval responses (this calls the Anthropic API)..."
cd "$BACKEND_DIR"
EVAL_CACHE_MODE=Record dotnet test RunCoach.slnx --filter "Category=Eval" --no-restore
echo "  Recording complete."

# Step 3: Post-process TTL to 9999-12-31
echo "[4/5] Extending TTL on all entry.json files..."
PATCHED=0
while IFS= read -r -d '' entry_file; do
    if command -v python3 &>/dev/null; then
        python3 -c "
import json, sys
with open('$entry_file', 'r') as f:
    data = json.load(f)
data['expiration'] = '9999-12-31T23:59:59Z'
with open('$entry_file', 'w') as f:
    json.dump(data, f, indent=2)
"
    else
        # Fallback: sed replacement
        sed -i.bak 's/"expiration": "[^"]*"/"expiration": "9999-12-31T23:59:59Z"/' "$entry_file"
        rm -f "${entry_file}.bak"
    fi
    PATCHED=$((PATCHED + 1))
done < <(find "$CACHE_DIR" -name "entry.json" -print0)
echo "  Patched $PATCHED entry.json files."

# Step 4: Verify Replay mode works
echo "[5/5] Verifying Replay mode with new fixtures..."
EVAL_CACHE_MODE=Replay dotnet test RunCoach.slnx --filter "Category=Eval" --no-restore
echo ""
echo "=== Done. Cache is fresh and TTL-extended. ==="
echo "Next: git add backend/tests/eval-cache/ && git commit"
