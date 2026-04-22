# Research Prompt: Batch 19d — R-059

# Timing-safety of `SignInManager.PasswordSignInAsync` on unknown email in ASP.NET Core Identity 10 (2026)

Copy the prompt below and hand to your deep research agent.

---

## PROMPT

**Research Topic:** In ASP.NET Core Identity 10 (2026), does `SignInManager<TUser>.PasswordSignInAsync(userName, password, isPersistent, lockoutOnFailure)` internally perform a constant-time dummy password-hash check when the user is not found, so the failure path is timing-safe against user-enumeration attacks? Or must a custom `AuthController.Login` endpoint implement the mitigation manually (e.g., `UserManager.FindByEmailAsync` → dummy-hash verification when user is null)?

## Context

RunCoach's Slice 0 spec §Unit 2 line 86:

> "The system shall reject invalid login credentials with HTTP 401 and a generic `ProblemDetails` body that does not disclose whether the email exists. Implementation shall use `SignInManager.PasswordSignInAsync` (**or** `UserManager.FindByEmailAsync` followed by a dummy hash check) so the failure path is timing-safe."

The "or" suggests the spec author wasn't certain `PasswordSignInAsync` is timing-safe out of the box and wrote the fallback mitigation. T02.4 writes the `AuthController.Login` endpoint and must pick one implementation path.

Historical concern: a naive `FindByEmailAsync(email)` → `null` → return 401 path leaks timing — password hashing (BCrypt / PBKDF2 / argon2 on the correct user) takes tens to hundreds of milliseconds; an immediate null-return is microseconds. An attacker timing the response distinguishes "user exists" from "user doesn't exist" and enumerates the user table.

The ASP.NET Identity source history suggests the older `SignInManager` did NOT have built-in timing-safety — the null-user path returned `SignInResult.Failed` without any hash work. Whether the .NET 10 (2026) `SignInManager` changed this is unclear.

The spec's fallback pattern is:

```csharp
var user = await userManager.FindByEmailAsync(email);
if (user is null)
{
    // dummy hash to match the timing of a real password verification
    await userManager.PasswordHasher.HashPasswordAsync(default, password);
    return Unauthorized(GenericProblemDetails());
}
var result = await signInManager.PasswordSignInAsync(user, password, isPersistent: true, lockoutOnFailure: false);
```

## Research Question

**Primary:** Is `SignInManager.PasswordSignInAsync` in ASP.NET Core Identity 10 timing-safe on the unknown-user path, or does the login endpoint need an explicit dummy-hash mitigation?

**Sub-questions:**

1. **.NET 10 `SignInManager` source.** Trace `PasswordSignInAsync` in `dotnet/aspnetcore` main branch — does the null-user path now invoke a no-op / dummy-hash call? If yes, as of which .NET version did this land?
2. **Authoritative guidance.** What do Microsoft Learn docs / Andrew Lock / Duende / OWASP ASVS say about the 2026 canonical pattern? Does the ASP.NET Identity team treat user enumeration as a known issue with a documented mitigation, or do they consider `PasswordSignInAsync` sufficient?
3. **Dummy-hash mitigation correctness.** If manual mitigation is needed: what's the right call? `PasswordHasher.HashPasswordAsync(default, password)` vs `VerifyHashedPassword(default, knownFakeHash, password)` vs a dedicated `TimingAttackGuard` helper? What's the runtime-cost parity requirement?
4. **Lockout interaction.** `SignInManager.PasswordSignInAsync(..., lockoutOnFailure: false)` is the Slice 0 setting (account lockout deferred to MVP-1 per spec Non-Goals). When lockout lands, does the mitigation change?
5. **Integration-test assertion.** Can T02.5 assert the timing-safety invariant meaningfully, or is it inherently a runtime measurement that belongs in a load-test / security-audit harness? Any known test patterns?
6. **`UserManager.FindByEmailAsync` + normalization.** When email normalization is active (default), `FindByEmailAsync` normalizes the input before query. Any timing-leak on the normalization path itself?

## Why It Matters

Login is the only endpoint whose failure semantics are observable to unauthenticated attackers. A user-enumeration primitive here undermines the whole auth substrate — it lets an attacker build a list of valid email addresses before any credential-stuffing attempt.

The `SignInManager` API is well-known; picking the wrong interpretation bakes a security-sensitive bug into the first real login implementation the project ships. Getting the answer once beats a pentest finding six months in.

This is also the kind of security invariant that belongs in the decision log, not just in the controller code — future contributors tweaking the login path need the reasoning visible.

## Deliverables

- **Yes/no on built-in timing-safety** with primary-source evidence (aspnetcore source lines or Microsoft docs, not secondary articles).
- **Concrete `AuthController.Login` snippet** matching the recommendation — either "`SignInManager.PasswordSignInAsync` is sufficient" or the manual mitigation with the correct dummy-hash call.
- **Security audit note** — any additional timing / enumeration leaks in the surrounding request path worth closing at the same time.
- **Test assertion guidance** — whether T02.5 can meaningfully cover this and how.
- **Alternatives considered and why rejected.**

---

## Current Repo State

- `backend/src/RunCoach.Api/Program.cs` — Identity Core registration + password policy + `SignInManager` registered via `.AddSignInManager()` (T02.1, commit `08ef0c7`).
- Spec: `docs/specs/12-spec-slice-0-foundation/12-spec-slice-0-foundation.md` §Unit 2 line 86 for the stated requirement.
- T02.4 (task #54) is the next task — writes `AuthController.Login`. R-059's answer belongs in T02.4's spec before implementation begins.
