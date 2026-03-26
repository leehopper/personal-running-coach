#!/usr/bin/env bash
set -euo pipefail

# Pre-commit: when a NEW eval cache entry is staged, delete sibling hash dirs
# in the same scenario (they're stale from prior recordings).
#
# The staged diff is the oracle — if you're adding hash dir X to scenario S,
# then all other hash dirs in S are stale by definition.
#
# Called by lefthook pre-commit hook when eval-cache files are staged.

# Find staged NEW eval cache files (Added)
STAGED_NEW=$(git diff --cached --name-only --diff-filter=A | grep 'eval-cache.*entry\.json$' || true)

if [ -z "$STAGED_NEW" ]; then
    exit 0
fi

CLEANED=0

# For each newly staged entry, find its scenario dir and remove sibling hashes
for entry in $STAGED_NEW; do
    # entry path: backend/tests/eval-cache/{model}/cache/{scenario}/1/{hash}/entry.json
    hash_dir=$(dirname "$entry")
    new_hash=$(basename "$hash_dir")
    version_dir=$(dirname "$hash_dir")

    # Find all sibling hash dirs in this scenario
    for sibling in "$version_dir"/*/; do
        sibling_hash=$(basename "$sibling")
        if [ "$sibling_hash" != "$new_hash" ] && [ -d "$sibling" ]; then
            git rm -rf --quiet "$sibling" 2>/dev/null || true
            CLEANED=$((CLEANED + 1))
            scenario=$(echo "$version_dir" | sed 's|.*/eval-cache/||')
            echo "Removed stale: $scenario/$sibling_hash"
        fi
    done
done

if [ "$CLEANED" -gt 0 ]; then
    echo "Cleaned $CLEANED stale eval cache entries."
fi
