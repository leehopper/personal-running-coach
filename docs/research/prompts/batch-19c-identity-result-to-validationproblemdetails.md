# Research Prompt: Batch 19c — R-058

# Translating `IdentityResult.Errors` into `ValidationProblemDetails` keyed by DTO property (ASP.NET Core 10, 2026)

Copy the prompt below and hand to your deep research agent.

---

## PROMPT

**Research Topic:** For an ASP.NET Core 10 `[ApiController]`-decorated `AuthController` that calls `UserManager.CreateAsync` and receives `IdentityResult.Failed(IEnumerable<IdentityError>)`, what is the 2026 idiomatic pattern for translating those `IdentityError` entries into an RFC 7807 `ValidationProblemDetails` response whose `errors` dictionary is keyed by the **request DTO property name** (e.g., `password`, `email`) — not by the `IdentityError.Code` — while preserving the `[ApiController]` auto-400 behavior and working with the built-in `ProblemDetailsFactory`?

## Context

RunCoach's Slice 0 spec (§Unit 2 line 84) mandates that the 400 response to a weak-password registration puts errors under the DTO property key:

> "The system shall reject weak-password registration with HTTP 400 and a `ValidationProblemDetails` body whose `errors` dictionary lists failing rules. **The error key shall be the request property name (e.g., `password`), not the `IdentityResult` error code** (since this is a custom controller, the project's normal error contract applies)."

`AddProblemDetails()` is registered and a custom `IExceptionHandler` (T02.3) already wires the 500 path. The 400 path is unmodified — it relies on `[ApiController]`'s `ProblemDetailsFactory` to convert `ModelState` into `ValidationProblemDetails` when DTO DataAnnotations fail.

The gap: `IdentityResult.Errors` is NOT `ModelState`. `UserManager.CreateAsync` is an async service call; its failures don't go through MVC model-binding and aren't translated to ValidationProblemDetails by the framework. The controller must either (a) push errors into `ModelState` before returning, (b) build a `ValidationProblemDetails` manually, (c) pre-validate the DTO before calling `UserManager` so the Identity failures are rare, or (d) use a library that does the translation.

Password rules in this project are `RequiredLength = 12` + `RequireUppercase` + `RequireLowercase` + `RequireDigit` + `RequireNonAlphanumeric`. Identity emits multiple errors per failed password (one per failing rule) with codes like `PasswordTooShort`, `PasswordRequiresUpper`, etc. The desired SPA-facing shape is:

```json
{
  "type": "...",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "password": [
      "Passwords must be at least 12 characters.",
      "Passwords must have at least one uppercase ('A'-'Z').",
      "Passwords must have at least one non-alphanumeric character."
    ]
  }
}
```

Duplicate-email registration should return 409 with a `ProblemDetails` (not `ValidationProblemDetails`) whose `title` describes the conflict — that path is clearer and probably separate.

## Research Question

**Primary:** What's the 2026 canonical pattern for translating `IdentityResult.Errors` into a `ValidationProblemDetails` keyed by DTO property in an ASP.NET Core 10 `[ApiController]`?

**Sub-questions:**

1. **Manual `ModelState.AddModelError` loop.** How does the idiomatic `foreach (var error in result.Errors) ModelState.AddModelError("password", error.Description);` pattern compose with `[ApiController]` auto-400 behavior? Does returning `ValidationProblem(ModelState)` produce the right `ValidationProblemDetails` shape through `ProblemDetailsFactory`?
2. **`IdentityError.Code` classification.** What's the robust 2026 mapping from Identity's error codes to DTO property names? Are the codes stable across Identity versions? Any library / source-generated helper for this (aspnet-contrib, Duende, ASP.NET samples)?
3. **Pre-validate the DTO.** Would a Zod-mirrored DataAnnotations or FluentValidation pass on `RegisterRequestDto` before calling `UserManager` avoid the Identity-error-translation problem for the common case (weak password) and leave Identity errors only for the edge cases (duplicate email / database errors)?
4. **`ProblemDetailsFactory` custom writer.** Is there a cleaner path using `IProblemDetailsWriter` or a `ProblemDetailsFactory` override that intercepts the `IdentityResult` and shapes the response once?
5. **Duplicate email: 409 `ProblemDetails` contract.** Best-practice shape for `IdentityError` code `DuplicateEmail` / `DuplicateUserName` → 409 with a `ProblemDetails.Title` describing the conflict. Is this distinct from the weak-password 400 path or can both flow through the same translator?
6. **Typed exception-to-ProblemDetails alternative.** Would a `RegistrationFailedException` thrown from a service and caught by a per-exception `IExceptionHandler` (next to T02.3's `ErrorHandlingMiddleware`) be cleaner than in-controller translation? Any precedent in eShop / Ardalis / Duende samples?

## Why It Matters

T02.4 writes the `AuthController` endpoints. T03.x's login / register forms consume the error contract. Getting the shape right once — before T02.5 integration tests pin the contract and T03.x form fields bind to the `errors["password"]` / `errors["email"]` keys — beats two PRs to reshape every call site later.

The decision also sets the pattern for every future controller that turns a service-layer result into a `ValidationProblemDetails`: plan adaptation failures (Slice 3), workout log validation errors (Slice 2), conversation safety blocks (Slice 4). Land the idiom in Slice 0 so the next four slices don't reinvent it.

## Deliverables

- **Primary recommendation** with a concrete controller-action snippet for both the 400 weak-password path and the 409 duplicate-email path.
- **`IdentityError.Code` → DTO-property-name mapping** that's stable across Identity versions, as a small reusable helper.
- **Pre-validation decision** — should DTO DataAnnotations / FluentValidation catch the common cases before `UserManager`, and if so what belongs to the DTO vs what genuinely lives in Identity?
- **Response-body JSON examples** for weak-password (400), invalid-login (401, generic), and duplicate-email (409) so T03.x has a concrete target to bind against.
- **Alternatives considered and why rejected.**

---

## Current Repo State

- `backend/src/RunCoach.Api/Program.cs` — Identity Core registration + password policy (commit `08ef0c7`, T02.1); `AddProblemDetails()` + `ErrorHandlingMiddleware` (commit `5c86657`, T02.3).
- `backend/src/RunCoach.Api/Infrastructure/ErrorHandlingMiddleware.cs` — 500 path, RFC 7807, verbose detail in Dev.
- Spec: `docs/specs/12-spec-slice-0-foundation/12-spec-slice-0-foundation.md` §Unit 2 lines 82–90 for the exact contract.
- Frontend spec: `docs/specs/12-spec-slice-0-foundation/frontend-auth-ux.feature` for the SPA side of the contract.
