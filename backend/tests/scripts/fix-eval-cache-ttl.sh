#!/usr/bin/env bash
set -euo pipefail

# Pre-commit auto-fix: patches any staged eval cache entry.json files
# to use 9999-12-31 TTL per DEC-039, then restages them.
#
# Called by lefthook pre-commit hook when eval-cache files are staged.

STAGED_ENTRIES=$(git diff --cached --name-only --diff-filter=ACM | grep 'eval-cache.*entry\.json$' || true)

if [ -z "$STAGED_ENTRIES" ]; then
    exit 0
fi

FIXED=0
for entry in $STAGED_ENTRIES; do
    if ! grep -q '"9999-12-31' "$entry" 2>/dev/null; then
        sed -i '' 's/"expiration": "[^"]*"/"expiration": "9999-12-31T23:59:59Z"/' "$entry"
        git add "$entry"
        FIXED=$((FIXED + 1))
    fi
done

if [ "$FIXED" -gt 0 ]; then
    echo "Auto-fixed TTL to 9999-12-31 on $FIXED entry.json file(s)."
fi
