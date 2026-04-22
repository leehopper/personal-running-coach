# Contributing to RunCoach

This file is the developer loop reference — everything you need to bring the
stack up locally and exercise every auth endpoint from Swagger UI or a CLI
tool. For higher-level project context (vision, architecture, roadmap) start
at `README.md`, `CLAUDE.md`, and `ROADMAP.md`.

Frontend-specific developer setup (Vite dev server, `/api` proxy, Playwright
page-level E2E) is covered by the frontend contribution section — tracked
under task T03.0 and landing with the Unit 3 frontend slice.

## Local HTTPS is required

Browsers silently drop cookies whose name starts with `__Host-` unless they
arrive over HTTPS with the `Secure` flag set and a `Path=/` attribute. The
RunCoach auth contract uses three such cookies:

- `__Host-RunCoach` — the application session cookie (Identity cookie).
- `__Host-Xsrf` — the antiforgery request-verification cookie.
- `__Host-Xsrf-Request` — the SPA-readable antiforgery request cookie.

Running the API over plain HTTP means the browser silently drops all three on
every response. Login appears to succeed (HTTP 200) but every follow-up
request logs the user back out, which is easy to mistake for a server bug.

Plain HTTP is still fine for CLI tools that track cookies manually — see the
curl recipe at the bottom of this file — because no browser cookie jar is
involved. Everything browser-based (Swagger UI, the Vite SPA, DevTools)
requires HTTPS.

## One-time setup

These steps run once per machine. They set up the local dev cert, the Docker
Compose secret, and the connection-string user secret.

### 1. Trust the ASP.NET Core dev cert

```bash
dotnet dev-certs https --trust
```

On macOS this prompts for your keychain password. On Windows it prompts for
admin elevation. On corporate Linux workstations `--trust` sometimes fails
against the CA policy — use `mkcert -install` as an escape hatch in that
case. Once trusted, `dotnet run --launch-profile https` serves trusted HTTPS
without a browser warning.

### 2. Export the cert for the Docker `api` service

The `api` container loads the same dev cert from a bind-mounted directory:

```bash
mkdir -p ~/.aspnet/https
dotnet dev-certs https \
    --export-path ~/.aspnet/https/aspnetapp.pfx \
    -p "<choose a password>"
```

### 3. Create `.env` for Docker Compose

`.env` is gitignored. Compose reads it automatically. Set the password to
match the one you chose in the export step:

```bash
cat > .env <<EOF
ASPNETCORE_HTTPS_PASSWORD=<the password you chose in step 2>
EOF
```

`docker-compose.yml` fails fast with a clear error if this variable is
missing, so you will know immediately if the file is absent.

### 4. Set the Anthropic + database user secrets

Per DEC-046, secrets never land in `appsettings.*.json`. `dotnet
user-secrets` is the storage mechanism for host-run configuration:

```bash
dotnet user-secrets set "ConnectionStrings:runcoach" \
    "Host=localhost;Port=5432;Database=runcoach;Username=runcoach;Password=runcoach_dev" \
    --project backend/src/RunCoach.Api

dotnet user-secrets set "Anthropic:ApiKey" "sk-ant-..." \
    --project backend/src/RunCoach.Api
```

The test project uses a separate user-secrets store — see
`backend/CLAUDE.md` for the `runcoach-api-tests` store instructions.

## Running the stack

Two supported paths. Pick based on what you are iterating on.

### Path A — `tilt up` (full stack in Compose)

```bash
tilt up
```

Serves the API at `https://localhost:5001/swagger` with Postgres, Redis,
pgAdmin, and the Aspire dashboard all brought up together.

### Path B — host-run API + infra in Compose (primary per DEC-045 / R-050)

Fastest inner loop. Infrastructure runs in Docker, the API runs on the host
with hot-reload and a normal debugger attached.

```bash
docker compose up -d postgres redis

cd backend
dotnet run --project src/RunCoach.Api --launch-profile https
```

Swagger UI lands at `https://localhost:5001/swagger`. The `https` launch
profile auto-opens a browser tab.

Either path leaves the five auth endpoints at
`https://localhost:5001/api/v1/auth/*`.

## Exercising the auth endpoints

### Swagger UI

Swagger UI's browser-side interceptor auto-attaches the `X-XSRF-TOKEN`
header on every unsafe request, so you never need to open DevTools and copy
the cookie value by hand. Click through:

1. `GET /api/v1/auth/xsrf` → 204 No Content. DevTools → Application →
   Cookies should now show `__Host-Xsrf` and `__Host-Xsrf-Request` both set
   and both marked `Secure`.
2. `POST /api/v1/auth/register` with a body like
   `{"email":"you@example.com","password":"SomethingLong123!"}` → 201
   Created.
3. `POST /api/v1/auth/login` with the same credentials → 200 OK. DevTools
   now shows `__Host-RunCoach` alongside the two antiforgery cookies.
4. `GET /api/v1/auth/me` → 200 OK with the user summary.
5. `POST /api/v1/auth/logout` → 204 No Content. `__Host-RunCoach` has been
   cleared in DevTools.
6. `GET /api/v1/auth/me` again → 401 Unauthorized. The session really is
   gone.

If the `X-XSRF-TOKEN` header ever shows up missing on an unsafe request,
check that you called `GET /xsrf` at least once in the session — the
interceptor no-ops when the cookie is absent so the xsrf endpoint itself is
never corrupted.

### curl recipe (also works for Postman, Bruno, httpie — any tool with a
cookie jar)

Plain HTTP is fine here because curl's cookie jar has no `__Host-` rules.
Run against the HTTP listener on port 5000 if you prefer; the flow is the
same over HTTPS on 5001.

```bash
JAR=$(mktemp)

# 1. Prime the antiforgery cookies.
curl -sS -k -c "$JAR" -b "$JAR" \
    -X GET https://localhost:5001/api/v1/auth/xsrf

# 2. Read the SPA-readable antiforgery token out of the jar.
XSRF=$(awk '/__Host-Xsrf-Request/ {print $7}' "$JAR")

# 3. Register.
curl -sS -k -c "$JAR" -b "$JAR" \
    -H "Content-Type: application/json" \
    -H "X-XSRF-TOKEN: $XSRF" \
    -X POST https://localhost:5001/api/v1/auth/register \
    -d '{"email":"you@example.com","password":"SomethingLong123!"}'

# 4. Log in.
curl -sS -k -c "$JAR" -b "$JAR" \
    -H "Content-Type: application/json" \
    -H "X-XSRF-TOKEN: $XSRF" \
    -X POST https://localhost:5001/api/v1/auth/login \
    -d '{"email":"you@example.com","password":"SomethingLong123!"}'

# 5. Call the authenticated endpoint.
curl -sS -k -b "$JAR" \
    -X GET https://localhost:5001/api/v1/auth/me

# 6. Log out.
curl -sS -k -c "$JAR" -b "$JAR" \
    -H "X-XSRF-TOKEN: $XSRF" \
    -X POST https://localhost:5001/api/v1/auth/logout
```

The `-k` flag accepts the self-signed dev cert. The `-c`/`-b` pair reads and
writes the same jar file so cookies survive across requests.

## Post-change checks

See the root `CLAUDE.md` "Post-Change Checklist" for the standard build +
test gates.
