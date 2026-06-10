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

# Step 0: Verify API key is available in the TEST project's user-secrets
# (The test project has UserSecretsId=runcoach-api-tests, separate from the API project's runcoach-api)
echo "[1/5] Checking API key..."
TEST_PROJECT="$BACKEND_DIR/tests/RunCoach.Api.Tests"
if ! dotnet user-secrets list --project "$TEST_PROJECT" 2>/dev/null | grep -q "Anthropic:ApiKey"; then
    if [ -n "$ANTHROPIC_API_KEY" ]; then
        echo "  No key in test user-secrets, but ANTHROPIC_API_KEY env var is set (will be used as fallback)."
    else
        echo "ERROR: No Anthropic API key found."
        echo "Set it with: dotnet user-secrets set \"Anthropic:ApiKey\" \"<key>\" --project backend/tests/RunCoach.Api.Tests"
        echo "Or export ANTHROPIC_API_KEY in your shell."
        exit 1
    fi
else
    echo "  API key found in test user-secrets."
fi

# Step 1: Delete existing cache
echo "[2/5] Deleting existing cache at $CACHE_DIR..."
rm -rf "$CACHE_DIR"
echo "  Deleted."

# Step 2: Record fresh
echo "[3/5] Recording fresh eval responses (this calls the Anthropic API)..."
cd "$BACKEND_DIR"
# Invoke the test binary directly — matches the lefthook dotnet-test pattern.
# xunit v3's native CLI accepts `-trait "Category=Eval"` as the inclusion
# filter; the legacy VSTest `--filter` syntax is silently ignored on MTP.
dotnet build RunCoach.slnx --no-restore >/dev/null
EVAL_CACHE_MODE=Record "$BACKEND_DIR/tests/RunCoach.Api.Tests/bin/Debug/net10.0/RunCoach.Api.Tests" -trait "Category=Eval"
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

# Step 3b: Regenerate the DEC-074 prompt-hash sentinel manifest so the committed
# cache and the manifest always move together (the manifest records the prompt
# contents this cache was recorded against).
echo "[4b/5] Regenerating prompt-hash manifest (DEC-074)..."
bash "$SCRIPT_DIR/check-prompt-hashes.sh" --write

# Step 4: Verify Replay mode works
echo "[5/5] Verifying Replay mode with new fixtures..."
EVAL_CACHE_MODE=Replay "$BACKEND_DIR/tests/RunCoach.Api.Tests/bin/Debug/net10.0/RunCoach.Api.Tests" -trait "Category=Eval"
echo ""
echo "=== Done. Cache is fresh and TTL-extended. ==="
echo "Next: git add backend/tests/eval-cache/ backend/src/RunCoach.Api/Prompts/.prompt-hashes.sha256 && git commit"
