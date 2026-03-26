#!/usr/bin/env bash
set -euo pipefail

# Pre-commit check: ensures any staged eval cache entry.json files
# have the 9999-12-31 TTL per DEC-039. Prevents committing entries
# with the default 14-day TTL that would silently expire in CI.
#
# Called by lefthook pre-commit hook.

STAGED_ENTRIES=$(git diff --cached --name-only --diff-filter=ACM | grep 'eval-cache.*entry\.json$' || true)

if [ -z "$STAGED_ENTRIES" ]; then
    exit 0
fi

FAILURES=0
for entry in $STAGED_ENTRIES; do
    # Read the staged version, not the working tree version
    EXPIRATION=$(git show ":$entry" | grep -o '"expiration": "[^"]*"' | head -1)
    if [ -z "$EXPIRATION" ]; then
        echo "WARNING: $entry has no expiration field"
        FAILURES=$((FAILURES + 1))
    elif ! echo "$EXPIRATION" | grep -q "9999-12-31"; then
        echo "BLOCKED: $entry has short TTL — run backend/tests/scripts/rerecord-eval-cache.sh"
        echo "  Found: $EXPIRATION"
        echo "  Expected: \"expiration\": \"9999-12-31T23:59:59Z\""
        FAILURES=$((FAILURES + 1))
    fi
done

if [ "$FAILURES" -gt 0 ]; then
    echo ""
    echo "ERROR: $FAILURES eval cache entry.json file(s) have incorrect TTL."
    echo "The default 14-day TTL will silently expire in CI (DEC-039)."
    echo "Fix: run backend/tests/scripts/rerecord-eval-cache.sh"
    exit 1
fi
