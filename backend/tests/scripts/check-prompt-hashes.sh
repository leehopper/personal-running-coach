#!/usr/bin/env bash
set -euo pipefail

# DEC-074: SHA-256 sentinel binding the committed eval cache to the prompt YAMLs.
#
# A prompt edit busts the M.E.AI response-cache key (prompt text travels inside
# messages[]), so a stale committed cache surfaces as a Replay MISS in CI. This
# sentinel surfaces the same drift EARLIER — at commit time (the glob-scoped
# lefthook pre-commit hook) — and as a CI / --no-verify backstop (the
# EvalTestBase static constructor recomputes these same hashes).
#
# Manifest format (must match the EvalTestBase C# backstop byte-for-byte):
#   one "<64 lowercase hex>  <bare filename>\n" line per Prompts/*.yaml,
#   filename-sorted in C locale, lowercase hex, two spaces, trailing newline.
#
# Usage:
#   check-prompt-hashes.sh            # verify the committed manifest is in sync (exit 1 on drift)
#   check-prompt-hashes.sh --write    # regenerate the committed manifest (called by rerecord-eval-cache.sh)

export LC_ALL=C

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
BACKEND_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
PROMPTS_DIR="$BACKEND_DIR/src/RunCoach.Api/Prompts"
MANIFEST="$PROMPTS_DIR/.prompt-hashes.sha256"

# Normalize the hash tool across platforms: sha256sum (Linux) vs shasum (macOS).
if command -v sha256sum >/dev/null 2>&1; then
    HASH_CMD=(sha256sum)
elif command -v shasum >/dev/null 2>&1; then
    HASH_CMD=(shasum -a 256)
else
    echo "ERROR: neither sha256sum nor shasum is available." >&2
    exit 1
fi

# Emits the canonical manifest text to stdout (no trailing-newline guarantee from
# command substitution callers; the --write branch re-appends one).
compute_manifest() {
    (
        cd "$PROMPTS_DIR"
        # C-locale glob expansion is ordinal-sorted, matching the C# StringComparer.Ordinal backstop.
        for yaml in *.yaml; do
            "${HASH_CMD[@]}" "$yaml"
        done
    )
}

ACTUAL="$(compute_manifest)"

if [ "${1:-}" = "--write" ]; then
    printf '%s\n' "$ACTUAL" > "$MANIFEST"
    echo "Wrote prompt-hash manifest: $MANIFEST"
    exit 0
fi

if [ ! -f "$MANIFEST" ]; then
    echo "ERROR: prompt-hash manifest missing ($MANIFEST) — DEC-074." >&2
    echo "Fix: run backend/tests/scripts/rerecord-eval-cache.sh" >&2
    exit 1
fi

EXPECTED="$(cat "$MANIFEST")"

if [ "$ACTUAL" != "$EXPECTED" ]; then
    echo "ERROR: a prompt under Prompts/ changed but the eval cache was not re-recorded (DEC-074)." >&2
    echo "The committed eval cache no longer matches the prompt contents." >&2
    echo "Fix: run backend/tests/scripts/rerecord-eval-cache.sh" >&2
    exit 1
fi

echo "Prompt-hash manifest is in sync (DEC-074)."
