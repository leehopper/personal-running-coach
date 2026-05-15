#!/usr/bin/env bash
# Positive-failure test for the codegen drift gate.
#
# The drift gate (`npm run codegen:check`) is the load-bearing seam that
# catches hand-rolled schema drifting from the wire contract (DEC-066 /
# R-071). This script verifies the gate actually FAILS when a generated
# file is dirty — proving the gate has teeth, not just that it passes on
# a clean tree.
#
# Protocol:
#   1. Append a sentinel line to a known generated file.
#   2. Run `npm run codegen:check`; capture exit code.
#   3. Restore the file unconditionally on EXIT (trap).
#   4. Assert the captured exit code was non-zero → report PASS/FAIL.
#
# The trap only reverts the one specific file we touched, so a dirty
# working tree is left undisturbed for everything else.

set -uo pipefail

SENTINEL="// drift-gate-positive-failure-test-sentinel"
# Target the hand-maintained barrel — codegen does not touch this file, so
# the sentinel survives `npm run codegen` and is detectable by the trailing
# `git diff --exit-code` check inside `codegen:check`. Targeting an
# auto-regenerated file (e.g. `rtk/api.ts`) would silently erase the
# sentinel during the codegen step and leave nothing for the gate to catch.
TARGET="src/app/api/generated/index.ts"

# Resolve the directory containing this script so the script works
# regardless of where `npm run` invokes it from.
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
FRONTEND_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"

# Restore exactly the sentinel file on exit — whether we succeed, fail,
# or are interrupted. Nothing outside TARGET is touched.
cleanup() {
  git -C "${FRONTEND_DIR}" checkout -- "${TARGET}" 2>/dev/null || true
}
trap cleanup EXIT

echo "[test-codegen-drift] Appending sentinel to ${TARGET}"
printf '\n%s\n' "${SENTINEL}" >> "${FRONTEND_DIR}/${TARGET}"

echo "[test-codegen-drift] Running codegen:check (expecting non-zero exit)..."
set +e
(cd "${FRONTEND_DIR}" && npm run codegen:check 2>&1)
GATE_EXIT=$?
set -e

if [ "${GATE_EXIT}" -ne 0 ]; then
  echo "[test-codegen-drift] PASS — drift gate correctly rejected a dirty generated file (exit ${GATE_EXIT})"
  exit 0
else
  echo "[test-codegen-drift] FAIL — drift gate reported success despite a modified generated file (exit 0)" >&2
  exit 1
fi
