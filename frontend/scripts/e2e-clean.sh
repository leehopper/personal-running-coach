#!/usr/bin/env bash
# Delete orphan e2e-*@runcoach.test accounts from the dev Postgres.
#
# The Playwright happy-path test registers a fresh
# `e2e-<uuid>@runcoach.test` on every run so re-runs never collide.
# Those rows accumulate in the dev DB over time. This script flushes
# them. Safe to run whenever — it only touches rows whose normalized
# email starts with `E2E-`.
#
# Runs against the docker-compose Postgres on localhost:5432 using the
# baked-in dev credentials (also visible in `docker-compose.yml`; not a
# secret — dev-only password). If you run Postgres somewhere else,
# export PGHOST / PGPORT / PGUSER / PGPASSWORD / PGDATABASE before
# invoking this script and the defaults will yield to them.

set -euo pipefail

: "${PGHOST:=localhost}"
: "${PGPORT:=5432}"
: "${PGUSER:=runcoach}"
: "${PGPASSWORD:=runcoach_dev}"
: "${PGDATABASE:=runcoach}"

export PGHOST PGPORT PGUSER PGPASSWORD PGDATABASE

exec psql --quiet --no-psqlrc --set ON_ERROR_STOP=1 <<'SQL'
DELETE FROM "AspNetUsers"
 WHERE "NormalizedEmail" LIKE 'E2E-%@RUNCOACH.TEST';
SQL
